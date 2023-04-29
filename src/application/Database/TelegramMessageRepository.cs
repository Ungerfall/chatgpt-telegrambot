using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
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
                Id = Guid.NewGuid(),
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
}
