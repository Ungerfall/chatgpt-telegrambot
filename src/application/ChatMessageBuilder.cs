using OpenAI.ObjectModels.RequestModels;
using System.Collections.Generic;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using Ungerfall.ChatGpt.TelegramBot.Database;

namespace Ungerfall.ChatGpt.TelegramBot;
public sealed class ChatMessageBuilder :
    // fluent interfaces
    IChatMessageBuilderTokenCounterState,
    IChatMessageBuilderSystemRoleState,
    IChatMessageBuilderAddMessagesState
{
    private const string BotUser = "chatgpt_ungerfall_bot";
    private const double MaxTokens = 4096 * 0.7; // .3 if left for a gpt response

    private ITokenCounter _tokenCounter = null!;
    private int _tokensSum;
    private List<ChatMessage> _message = new();

    private ChatMessageBuilder()
    {
    }

    public static IChatMessageBuilderTokenCounterState Create()
    {
        return new ChatMessageBuilder();
    }

    public IChatMessageBuilderSystemRoleState WithTokenCounter(ITokenCounter counter)
    {
        _tokenCounter = counter;
        return this;
    }

    public IChatMessageBuilderAddMessagesState WithSystemRoleMessage(string msg)
    {
        _message = new List<ChatMessage>
        {
            ChatMessage.FromSystem(msg),
        };
        _tokensSum = _tokenCounter.Count(msg);
        return this;
    }

    public IChatMessageBuilderAddMessagesState AddMessage(TelegramMessage message, int? index = null)
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

        _tokensSum += _tokenCounter.Count(message.Message);
        return this;
    }

    public IChatMessageBuilderAddMessagesState AddUserMessage(string message)
    {
        _message.Add(ChatMessage.FromUser(message));
        _tokensSum += _tokenCounter.Count(message);
        return this;
    }

    public ChatMessage[] Build()
    {
        return _message.ToArray();
    }

    public bool CanAddMessage => _tokensSum <= MaxTokens;

    public int TokensCount => _tokensSum;
}
