using OpenAI.ObjectModels.RequestModels;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using Ungerfall.ChatGpt.TelegramBot.Database;

namespace Ungerfall.ChatGpt.TelegramBot;
public interface IChatMessageBuilderTokenCounterState
{
    IChatMessageBuilderSystemRoleState WithTokenCounter(ITokenCounter? counter = null);
}
public interface IChatMessageBuilderSystemRoleState
{
    IChatMessageBuilderAddMessagesState WithSystemRoleMessage(string msg, string? lastUserMessage = null);
}
public interface IChatMessageBuilderAddMessagesState
{
    IChatMessageBuilderAddMessagesState AddMessage(TelegramMessage message, int? index = null);
    ChatMessage[] Build();
    bool CanAddMessage(TelegramMessage message);
    int TokensCount { get; }
    bool ContainsHistory { get; }
}
