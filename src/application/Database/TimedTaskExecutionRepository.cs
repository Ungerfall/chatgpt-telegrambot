using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
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
        var container = cosmos.GetContainer(_cosmosDbOptions.DatabaseId, _cosmosDbOptions.MessagesContainerId);
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
        var container = cosmos.GetContainer(_cosmosDbOptions.DatabaseId, _cosmosDbOptions.MessagesContainerId);
        DateTime startOfDay = date.Date;
        DateTime startOfNextDay = startOfDay.AddDays(1d);
        return await container.GetItemLinqQueryable<TimedTaskExecution>(
                allowSynchronousQueryExecution: true,
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(chatId) })
            .Where(x => x.ChatId == chatId && x.Name == name && x.DateUtc >= startOfDay && x.DateUtc < startOfNextDay)
            .CountAsync(cancellationToken: cancellation) > 0;
    }
}
