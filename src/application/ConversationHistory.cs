using OpenAI.GPT3.ObjectModels.RequestModels;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ungerfall.ChatGpt.TelegramBot.Database;

namespace Ungerfall.ChatGpt.TelegramBot;
public class ConversationHistory
{
    private const string BotUser = "chatgpt_ungerfall_bot";
    private const int MaxTokens = 4096;

    private readonly BriefTelegramMessageRepository _history;
    private readonly string _message;

    public ConversationHistory(BriefTelegramMessageRepository history, string message)
    {
        _history = history;
        _message = message;
    }

    public async Task<ChatMessage[]> GetForChatGpt(CancellationToken cancellation)
    {
        var messages = new List<ChatMessage>
        {
            ChatMessage.FromSystem("You are an AI that provides brief and concise answers.")
        };
        int tokensSum = 0;
        await foreach (var h in _history.GetAllOrderByDateDescending(cancellation))
        {
            tokensSum += CalculateTokens(h.Message);
            if (tokensSum >= MaxTokens)
            {
                break;
            }

            messages.Insert(
                1, // because of descending order of items in history
                h.User == BotUser
                    ? ChatMessage.FromAssistant($"{h.User}: {h.Message}")
                    : ChatMessage.FromUser($"{h.User}: {h.Message}"));
        }

        messages.Add(ChatMessage.FromUser(_message));
        return messages.ToArray();
    }

    private static int CalculateTokens(string msg)
    {
        // OpenAI.GPT3.Tokenizer.GPT3.TokenizerGpt3.TokenCount couldn't count Cyrillic at the moment.
        return msg.Count(char.IsWhiteSpace) + (msg.Length / 8); // why 8 though ???
    }
}
