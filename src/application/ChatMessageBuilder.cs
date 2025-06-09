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
    private const double MaxTokens = 4096 * 0.4; // .3 is left for a gpt response

    private ITokenCounter _tokenCounter = null!;
    private List<ChatMessage> _message = [];

    private ChatMessageBuilder()
    {
    }

    public static IChatMessageBuilderTokenCounterState Create()
    {
        return new ChatMessageBuilder();
    }

    public IChatMessageBuilderSystemRoleState WithTokenCounter(ITokenCounter? counter = null)
    {
        _tokenCounter = counter ?? new TokenCounter();
        return this;
    }

    public IChatMessageBuilderAddMessagesState WithSystemRoleMessage(string msg)
    {
        _message = [ChatMessage.CreateSystemMessage(msg)];
        TokensCount = _tokenCounter.Count(msg);
        return this;
    }

    public IChatMessageBuilderAddMessagesState AddMessage(TelegramMessage message, int? index = null)
    {
        if (index == null)
        {
            _message.Add(
                message.User == BotUser
                    ? ChatMessage.CreateAssistantMessage(message.Message)
                    : ChatMessage.CreateUserMessage($"{message.User}: {message.Message}"));
        }
        else
        {
            _message.Insert(
                index.Value,
                message.User == BotUser
                    ? ChatMessage.CreateAssistantMessage(message.Message)
                    : ChatMessage.CreateUserMessage($"{message.User}: {message.Message}"));
        }

        TokensCount += _tokenCounter.Count(message.Message);
        return this;
    }

    public IChatMessageBuilderAddMessagesState AddUserMessage(string message)
    {
        _message.Add(ChatMessage.CreateUserMessage(message));
        TokensCount += _tokenCounter.Count(message);
        return this;
    }

    public ChatMessage[] Build()
    {
        return [.. _message];
    }

    public bool CanAddMessage => TokensCount <= MaxTokens;

    public int TokensCount { get; private set; }

    public bool ContainsUserMessage => _message.Exists(x => x is UserChatMessage);
}
