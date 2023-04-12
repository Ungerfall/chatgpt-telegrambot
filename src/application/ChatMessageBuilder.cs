using OpenAI.GPT3.ObjectModels.RequestModels;
using System.Collections.Generic;
using Ungerfall.ChatGpt.TelegramBot.Database;

namespace Ungerfall.ChatGpt.TelegramBot;
public sealed class ChatMessageBuilder
{
    private const string BotUser = "chatgpt_ungerfall_bot";
    private const double MaxTokens = 4096 * 0.4; // currently tokens count differs to open ai response.

    private readonly TokenCounter _tokenCounter;

    private int _tokensSum;
    private List<ChatMessage> _message = new();

    public ChatMessageBuilder(TokenCounter tokenCounter)
    {
        _tokenCounter = tokenCounter;
    }

    public ChatMessageBuilder ForBriefAndConciseSystem()
    {
        const string msg = "You are an AI that provides brief and concise answers.";
        _message = new List<ChatMessage>
        {
            ChatMessage.FromSystem(msg),
        };
        _tokensSum = _tokenCounter.Count(msg);
        return this;
    }

    public ChatMessageBuilder AddMessage(BriefTelegramMessage message, int? index = null)
    {
        if (index == null)
        {
            _message.Add(
                message.User == BotUser
                    ? ChatMessage.FromAssistant($"{message.Message}")
                    : ChatMessage.FromUser($"{message.User}: {message.Message}"));
        }
        else
        {
            _message.Insert(
                index.Value,
                message.User == BotUser
                    ? ChatMessage.FromAssistant($"{message.Message}")
                    : ChatMessage.FromUser($"{message.User}: {message.Message}"));
        }

        return this;
    }

    public ChatMessageBuilder AddUserMessage(string message)
    {
        _message.Add(ChatMessage.FromUser(message));
        return this;
    }

    public ChatMessage[] Build()
    {
        return _message.ToArray();
    }

    public bool CanAddMessage => _tokensSum <= MaxTokens;

    public int TokensCount => _tokensSum;
}
