using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace Ungerfall.ChatGpt.TelegramBot.Worker.Services;

public class PollingUpdateHandler : IUpdateHandler
{
    private readonly UpdateHandler _updateHandler;

    public PollingUpdateHandler(UpdateHandler updateHandler)
    {
        _updateHandler = updateHandler;
    }

    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        return _updateHandler.HandlePollingErrorAsync(exception, cancellationToken);
    }

    public async Task HandleUpdateAsync(ITelegramBotClient _, Update update, CancellationToken cancellationToken)
    {
        await _updateHandler.Handle(update, cancellationToken);
    }
}