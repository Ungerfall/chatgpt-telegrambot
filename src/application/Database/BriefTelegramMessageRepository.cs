using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Ungerfall.ChatGpt.TelegramBot.Database;
public class BriefTelegramMessageRepository
{
    private readonly CosmosClient _cosmos;
    private readonly CosmosDbOptions _options;
    private readonly ILogger<BriefTelegramMessageRepository> _logger;

    public BriefTelegramMessageRepository(
        CosmosClient cosmos,
        IOptions<CosmosDbOptions> options,
        ILogger<BriefTelegramMessageRepository> logger)
    {
        _cosmos = cosmos;
        _options = options.Value;
        _logger = logger;
    }

    public async IAsyncEnumerable<BriefTelegramMessage> Get(DateOnly dateUtc, [EnumeratorCancellation] CancellationToken cancellation)
    {
        var container = _cosmos.GetContainer(_options.DatabaseId, _options.BriefMessagesContainerId);
        using var it = container.GetItemQueryIterator<BriefTelegramMessage>(requestOptions: new QueryRequestOptions
        {
            MaxConcurrency = 1,
            PartitionKey = new PartitionKey(dateUtc.ToString(BriefTelegramMessage.DATE_UTC_FORMAT)),
        });
        while (it.HasMoreResults)
        {
            FeedResponse<BriefTelegramMessage> response = await it.ReadNextAsync(cancellation);
            foreach (BriefTelegramMessage item in response)
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

    public async IAsyncEnumerable<BriefTelegramMessage> GetAllOrderByDateDescending([EnumeratorCancellation] CancellationToken cancellation)
    {
        var container = _cosmos.GetContainer(_options.DatabaseId, _options.BriefMessagesContainerId);
        using var it = container.GetItemQueryIterator<BriefTelegramMessage>(requestOptions: new QueryRequestOptions
        {
            MaxConcurrency = 1,
        });
        while (it.HasMoreResults)
        {
            FeedResponse<BriefTelegramMessage> response = await it.ReadNextAsync(cancellation);
            foreach (BriefTelegramMessage item in response)
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
