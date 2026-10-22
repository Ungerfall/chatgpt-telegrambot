using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;

namespace Ungerfall.ChatGpt.TelegramBot.Database;
public class TimedTaskExecutionRepository(
    CosmosClient cosmos,
    IOptions<CosmosDbOptions> options) : ITimedTaskExecutionRepository
{
    private readonly CosmosDbOptions _cosmosDbOptions = options.Value;

    public async Task Create(TimedTaskExecution timedTask, CancellationToken cancellation)
    {
        var container = cosmos.GetContainer(_cosmosDbOptions.DatabaseId, _cosmosDbOptions.TimedTasksContainerId);
        await container.CreateItemAsync(
            timedTask,
            new PartitionKey(timedTask.ChatId),
            new ItemRequestOptions
            {
                EnableContentResponseOnWrite = false,
            },
            cancellation);
    }

    public async Task Create<T>(IEnumerable<T> items, Func<T, long> chatIdSelector, CancellationToken cancellation)
    {
        var container = cosmos.GetContainer(_cosmosDbOptions.DatabaseId, _cosmosDbOptions.TimedTasksContainerId);
        foreach (var item in items)
        {
            await container.CreateItemAsync(
                item,
                new PartitionKey(chatIdSelector(item)),
                new ItemRequestOptions { EnableContentResponseOnWrite = false },
                cancellation);
        }
    }

    public async Task<bool> Exists(long chatId, string name, DateTime date, CancellationToken cancellation)
    {
        var container = cosmos.GetContainer(_cosmosDbOptions.DatabaseId, _cosmosDbOptions.TimedTasksContainerId);
        DateTime startOfDay = date.Date;
        DateTime startOfNextDay = startOfDay.AddDays(1d);
        var query = new QueryDefinition("""
            SELECT VALUE c
            FROM c
            WHERE c.chatId = @chatId
                AND c.name = @name
                AND c.date >= @start AND c.date < @end
            """)
            .WithParameter("@chatId", chatId)
            .WithParameter("@name", name)
            .WithParameter("@start", startOfDay)
            .WithParameter("@end", startOfNextDay);

        using var it = container.GetItemQueryIterator<TimedTaskExecution>(
            query,
            requestOptions: new QueryRequestOptions
            {
                MaxConcurrency = 1,
                PartitionKey = new PartitionKey(chatId)
            });
        while (it.HasMoreResults)
        {
            return (await it.ReadNextAsync(cancellation)).Count != 0;
        }

        return false;
    }
    public async Task<TimedTaskQuiz?> GetQuiz(long chatId, string type, CancellationToken cancellation)
    {
        var container = cosmos.GetContainer(_cosmosDbOptions.DatabaseId, _cosmosDbOptions.TimedTasksContainerId);
        var query = new QueryDefinition(@"SELECT * FROM c
                WHERE (NOT IS_DEFINED(c.posted) OR c.posted = false)
                    AND c.chatId = @chatId
                    AND c.type = @type
                ORDER BY c._ts
                OFFSET 0 LIMIT 1")
            .WithParameter("@type", type)
            .WithParameter("@chatId", chatId);
        using var it = container.GetItemQueryIterator<TimedTaskQuiz>(
            query,
            requestOptions: new QueryRequestOptions
            {
                MaxConcurrency = 1,
                PartitionKey = new PartitionKey(chatId),
            });
        while (it.HasMoreResults)
        {
            FeedResponse<TimedTaskQuiz> response = await it.ReadNextAsync(cancellation);
            return response.FirstOrDefault();
        }

        return null;
    }

    public async Task CompleteQuiz(TimedTaskQuiz quiz, CancellationToken cancellation)
    {
        var container = cosmos.GetContainer(_cosmosDbOptions.DatabaseId, _cosmosDbOptions.TimedTasksContainerId);
        quiz.Posted = true;
        quiz.DateUtc = DateTime.UtcNow;
        await container.ReplaceItemAsync(quiz, quiz.Id, new PartitionKey(quiz.ChatId), cancellationToken: cancellation);
    }

    public async Task<TldrCache> BeginTldr(long chatId, string id, CancellationToken cancellation)
    {
        var container = cosmos.GetContainer(_cosmosDbOptions.DatabaseId, _cosmosDbOptions.TimedTasksContainerId);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var cache = new TldrCache { Id = id, ChatId = chatId };
            try
            {
                var current = await container.ReadItemAsync<TldrCache>(id, new PartitionKey(chatId), cancellationToken: cancellation);
                cache = current.Resource;
                cache.ETag = current.ETag;
                if (cache.Summary != null) return cache;
                if (cache.LeaseUntilUtc > DateTime.UtcNow)
                    throw new InvalidOperationException("TLDR for this history is already being generated; retry later.");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { }

            cache.LeaseUntilUtc = DateTime.UtcNow.AddMinutes(10);
            try
            {
                var saved = cache.ETag == null
                    ? await container.CreateItemAsync(cache, new PartitionKey(chatId), cancellationToken: cancellation)
                    : await container.ReplaceItemAsync(cache, id, new PartitionKey(chatId),
                        new ItemRequestOptions { IfMatchEtag = cache.ETag }, cancellation);
                cache.ETag = saved.ETag;
                return cache;
            }
            catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed) { }
        }
        throw new InvalidOperationException("Unable to reserve TLDR generation; retry later.");
    }

    public async Task CompleteTldr(TldrCache cache, CancellationToken cancellation)
    {
        var container = cosmos.GetContainer(_cosmosDbOptions.DatabaseId, _cosmosDbOptions.TimedTasksContainerId);
        await container.ReplaceItemAsync(cache, cache.Id, new PartitionKey(cache.ChatId),
            new ItemRequestOptions { IfMatchEtag = cache.ETag, EnableContentResponseOnWrite = false }, cancellation);
    }

    public async Task ReleaseTldr(TldrCache cache, CancellationToken cancellation)
    {
        var container = cosmos.GetContainer(_cosmosDbOptions.DatabaseId, _cosmosDbOptions.TimedTasksContainerId);
        await container.DeleteItemAsync<TldrCache>(cache.Id, new PartitionKey(cache.ChatId),
            new ItemRequestOptions { IfMatchEtag = cache.ETag }, cancellation);
    }
}
