using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace Ungerfall.ChatGpt.TelegramBot.AzureFunction.Services;

public class PollingUpdateHandler : IUpdateHandler
{
    private readonly UpdateHandler _updateHandler;

    public PollingUpdateHandler(UpdateHandler updateHandler)
    {
        _updateHandler = updateHandler;
    }

    public Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public async Task HandleUpdateAsync(ITelegramBotClient _, Update update, CancellationToken cancellationToken)
    {
        await _updateHandler.Handle(update, cancellationToken);
    }
}