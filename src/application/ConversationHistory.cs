using OpenAI.GPT3.ObjectModels.RequestModels;
using System.Threading;
using System.Threading.Tasks;
using Ungerfall.ChatGpt.TelegramBot.Database;

namespace Ungerfall.ChatGpt.TelegramBot;
public class ConversationHistory
{
    private readonly BriefTelegramMessageRepository _history;
    private readonly string _message;
    private readonly TokenCounter _tokenCounter;

    public ConversationHistory(
        BriefTelegramMessageRepository history,
        string message,
        TokenCounter tokenCounter)
    {
        _history = history;
        _message = message;
        _tokenCounter = tokenCounter;
    }

    public async Task<(ChatMessage[], int tokens)> GetForChatGpt(CancellationToken cancellation)
    {
        var mb = ChatMessageBuilder.Create()
            .WithTokenCounter(_tokenCounter)
            .ForBriefAndConciseSystem();
        await foreach (var h in _history.GetAllOrderByDateDescending(cancellation))
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
