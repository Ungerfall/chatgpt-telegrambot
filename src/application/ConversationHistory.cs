using OpenAI.GPT3.ObjectModels.RequestModels;
using System.Threading;
using System.Threading.Tasks;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using Ungerfall.ChatGpt.TelegramBot.Database;

namespace Ungerfall.ChatGpt.TelegramBot;
public class ConversationHistory
{
    private readonly TelegramMessageRepository _history;
    private readonly string _message;
    private readonly ITokenCounter _tokenCounter;
    private readonly IWhitelist _whitelist;

    public ConversationHistory(
        TelegramMessageRepository history,
        string message,
        ITokenCounter tokenCounter,
        IWhitelist whitelist)
    {
        _history = history;
        _message = message;
        _tokenCounter = tokenCounter;
        _whitelist = whitelist;
    }

    public async Task<(ChatMessage[], int tokens)> GetForChatGpt(long chatId, CancellationToken cancellation)
    {
        var mb = ChatMessageBuilder.Create()
            .WithTokenCounter(_tokenCounter)
            .WithSystemRoleMessage(_whitelist.GetSystemRoleMessage(chatId));
        await foreach (var h in _history.GetAllOrderByDateDescending(chatId, cancellation))
        {
            if (!mb.CanAddMessage)
            {
                break;
            }

            mb.AddMessage(h, 1); // because of descending order of items in history
        }

        mb.AddUserMessage(_message);
        return (mb.Build(), mb.TokensCount);
    }
}
