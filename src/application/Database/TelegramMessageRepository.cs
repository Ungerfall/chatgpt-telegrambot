using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;

namespace Ungerfall.ChatGpt.TelegramBot.Database;
public class TelegramMessageRepository : ITelegramMessageRepository
{
    private readonly CosmosClient _cosmos;
    private readonly CosmosDbOptions _options;
    private readonly ILogger<TelegramMessageRepository> _logger;

    public TelegramMessageRepository(
        CosmosClient cosmos,
        IOptions<CosmosDbOptions> options,
        ILogger<TelegramMessageRepository> logger)
    {
        _cosmos = cosmos;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Create(TelegramMessage message, CancellationToken cancellation)
    {
        var container = _cosmos.GetContainer(_options.DatabaseId, _options.MessagesContainerId);
        var date = DateTime.UtcNow.ToString(TelegramMessage.DATE_UTC_FORMAT);
        await container.UpsertItemAsync(
            new TelegramMessage
            {
                Id = $"message:{message.MessageId}",
                ChatId = message.ChatId,
                User = message.User,
                UserId = message.UserId,
                MessageId = message.MessageId,
                Message = message.Message,
                Date = message.Date,
                DateUtc = date,
                TTL = TelegramMessage.TTL_SECONDS,
            },
            new PartitionKey(message.ChatId),
            new ItemRequestOptions
            {
                EnableContentResponseOnWrite = false,
            },
            cancellation);
    }

    public async IAsyncEnumerable<TelegramMessage> Get(long chatId, DateOnly dateUtc, [EnumeratorCancellation] CancellationToken cancellation)
    {
        var container = _cosmos.GetContainer(_options.DatabaseId, _options.MessagesContainerId);
        var query = new QueryDefinition("SELECT * FROM c WHERE c.dateUtc = @dateUtc ORDER BY c.date DESC")
            .WithParameter("@dateUtc", dateUtc.ToString(TelegramMessage.DATE_UTC_FORMAT));
        using var it = container.GetItemQueryIterator<TelegramMessage>(
            query,
            requestOptions: new QueryRequestOptions
            {
                MaxConcurrency = 1,
                PartitionKey = new PartitionKey(chatId),
            });
        while (it.HasMoreResults)
        {
            FeedResponse<TelegramMessage> response = await it.ReadNextAsync(cancellation);
            foreach (TelegramMessage item in response)
            {
                cancellation.ThrowIfCancellationRequested();
                yield return item;
            }

            if (response.Diagnostics != null)
            {
                _logger.LogWarning("Diagnostics: {diagnostics}", response.Diagnostics.ToString());
            }
        }
    }

    public async IAsyncEnumerable<TelegramMessage> GetAllOrderByDateDescending(long chatId, [EnumeratorCancellation] CancellationToken cancellation)
    {
        var container = _cosmos.GetContainer(_options.DatabaseId, _options.MessagesContainerId);
        var query = new QueryDefinition("SELECT * FROM c ORDER BY c.date DESC");
        using var it = container.GetItemQueryIterator<TelegramMessage>(
            query,
            requestOptions: new QueryRequestOptions
            {
                MaxConcurrency = 1,
                PartitionKey = new PartitionKey(chatId),
            });
        while (it.HasMoreResults)
        {
            FeedResponse<TelegramMessage> response = await it.ReadNextAsync(cancellation);
            foreach (TelegramMessage item in response)
            {
                cancellation.ThrowIfCancellationRequested();
                yield return item;
            }

            if (response.Diagnostics != null)
            {
                _logger.LogWarning("Diagnostics: {diagnostics}", response.Diagnostics.ToString());
            }
        }
    }
    public async IAsyncEnumerable<TelegramMessage> GetOldMessages(int minTokens, [EnumeratorCancellation] CancellationToken cancellation)
    {
        var container = _cosmos.GetContainer(_options.DatabaseId, _options.MessagesContainerId);
        var olderThanDate = DateTime.UtcNow.AddHours(-1d);
        var query = new QueryDefinition(@"SELECT * FROM c
                WHERE (NOT IS_DEFINED(c.isShrunk) OR c.isShrunk = false)
                    AND (NOT IS_DEFINED(c.shrinkProcessed) OR c.shrinkProcessed = false)
                    AND (NOT IS_DEFINED(c.shrinkSkippedBelowTokens) OR IS_NULL(c.shrinkSkippedBelowTokens) OR c.shrinkSkippedBelowTokens != @minTokens)
                    AND (NOT IS_DEFINED(c.shrinkLeaseUntilUtc) OR IS_NULL(c.shrinkLeaseUntilUtc) OR c.shrinkLeaseUntilUtc < @now)
                    AND TimestampToDateTime(c._ts * 1000) < @olderThanDate
                ORDER BY c._ts
                OFFSET 0 LIMIT 100")
            .WithParameter("@olderThanDate", olderThanDate)
            .WithParameter("@now", DateTime.UtcNow)
            .WithParameter("@minTokens", minTokens);
        using var it = container.GetItemQueryIterator<TelegramMessage>(
            query,
            requestOptions: new QueryRequestOptions
            {
                MaxConcurrency = 1,
            });
        while (it.HasMoreResults)
        {
            FeedResponse<TelegramMessage> response = await it.ReadNextAsync(cancellation);
            foreach (TelegramMessage item in response)
            {
                cancellation.ThrowIfCancellationRequested();
                yield return item;
            }

            if (response.Diagnostics != null)
            {
                _logger.LogWarning("Diagnostics: {diagnostics}", response.Diagnostics.ToString());
            }
        }
    }

    public async Task<bool> TryBeginShrink(TelegramMessage message, int tokens, int minTokens, CancellationToken cancellation)
    {
        var container = _cosmos.GetContainer(_options.DatabaseId, _options.MessagesContainerId);
        try
        {
            var current = await container.ReadItemAsync<TelegramMessage>(message.Id, new PartitionKey(message.ChatId), cancellationToken: cancellation);
            var stored = current.Resource;
            if (stored.Message != message.Message || stored.IsShrunk || stored.ShrinkProcessed
                || stored.ShrinkSkippedBelowTokens == minTokens || stored.ShrinkLeaseUntilUtc > DateTime.UtcNow)
                return false;

            stored.ShrinkSkippedBelowTokens = tokens < minTokens ? minTokens : null;
            stored.ShrinkLeaseUntilUtc = tokens < minTokens ? null : DateTime.UtcNow.AddMinutes(10);
            var saved = await container.ReplaceItemAsync(stored, stored.Id, new PartitionKey(stored.ChatId),
                new ItemRequestOptions { IfMatchEtag = current.ETag }, cancellation);
            message.ETag = saved.ETag;
            message.ShrinkLeaseUntilUtc = stored.ShrinkLeaseUntilUtc;
            return tokens >= minTokens;
        }
        catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed or HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public Task CompleteShrink(TelegramMessage message, bool processed, CancellationToken cancellation)
    {
        message.ShrinkProcessed = processed;
        message.ShrinkLeaseUntilUtc = null;
        return Update(message, cancellation);
    }

    public async Task Update(TelegramMessage message, CancellationToken cancellation)
    {
        var container = _cosmos.GetContainer(_options.DatabaseId, _options.MessagesContainerId);
        await container.ReplaceItemAsync(
            message,
            message.Id,
            new PartitionKey(message.ChatId),
            new ItemRequestOptions
            {
                EnableContentResponseOnWrite = false,
                IfMatchEtag = message.ETag,
            },
            cancellation);
    }
}
