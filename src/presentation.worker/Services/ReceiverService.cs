using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Ungerfall.ChatGpt.TelegramBot.Worker.Abstract;

namespace Ungerfall.ChatGpt.TelegramBot.Worker.Services;

// Compose Receiver and UpdateHandler implementation
public class ReceiverService : ReceiverServiceBase<PollingUpdateHandler>
{
    public ReceiverService(
        ITelegramBotClient botClient,
        PollingUpdateHandler updateHandler,
        ILogger<ReceiverServiceBase<PollingUpdateHandler>> logger)
        : base(botClient, updateHandler, logger)
    {
    }
}