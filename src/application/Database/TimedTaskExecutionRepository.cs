using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using System;
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
}
