using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Ungerfall.ChatGpt.TelegramBot.Database;

namespace Ungerfall.ChatGpt.TelegramBot.Commands;
public class TooLongDidnotReadToday
{
    private readonly BriefTelegramMessageRepository _history;
    private readonly TokenCounter _tokenCounter;

    public TooLongDidnotReadToday(BriefTelegramMessageRepository history, TokenCounter tokenCounter)
    {
        _history = history;
        _tokenCounter = tokenCounter;
    }

    public async Task<Message> Execute(CancellationToken cancellation)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var mb = ChatMessageBuilder.Create()
            .WithTokenCounter(_tokenCounter)
            .ForBriefAndConciseSystem();
        await foreach (var h in _history.Get(today, cancellation))
        {
            if (!mb.CanAddMessage)
            {
                break;
            }

            mb.AddMessage(h, 1); // because of descending order of items in history
        }

        var chatMessage = mb.Build();

        return new Message();
    }
}
