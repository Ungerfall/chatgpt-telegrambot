using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace Ungerfall.ChatGpt.TelegramBot.AzureFunction;
public class QueuedUpdateHandler
{
    private readonly UpdateHandler _updateHandler;
    private readonly ILogger<QueuedUpdateHandler> _logger;

    public QueuedUpdateHandler(ILogger<QueuedUpdateHandler> logger, UpdateHandler updateHandler)
    {
        _logger = logger;
        _updateHandler = updateHandler;
    }

    [Function("QueuedUpdateHandler")]
    public async Task Run([ServiceBusTrigger(Const.TGBOT_UPDATES, Connection = "ServiceBusConnection")] Update update)
    {
        _logger.LogInformation("C# ServiceBus queue trigger function processed message: {update}", update.Message);
        await _updateHandler.Handle(update, CancellationToken.None);
        _logger.LogInformation("C# ServiceBus queue trigger function finished");
    }
}
