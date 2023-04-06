using Microsoft.Extensions.Logging;
using System;
using Ungerfall.ChatGpt.TelegramBot.AzureFunction.Abstract;

namespace Ungerfall.ChatGpt.TelegramBot.AzureFunction.Services;

// Compose Polling and ReceiverService implementations
public class PollingService : PollingServiceBase<ReceiverService>
{
    public PollingService(IServiceProvider serviceProvider, ILogger<PollingService> logger)
        : base(serviceProvider, logger)
    {
    }
}