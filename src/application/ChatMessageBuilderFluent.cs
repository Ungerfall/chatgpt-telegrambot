using OpenAI.GPT3.ObjectModels.RequestModels;
using Ungerfall.ChatGpt.TelegramBot.Database;

namespace Ungerfall.ChatGpt.TelegramBot;
public interface IChatMessageBuilderTokenCounterState
{
    IChatMessageBuilderSystemRoleState WithTokenCounter(TokenCounter counter);
}
public interface IChatMessageBuilderSystemRoleState
{
    IChatMessageBuilderAddMessagesState ForBriefAndConciseSystem();
}
public interface IChatMessageBuilderAddMessagesState
{
    IChatMessageBuilderAddMessagesState AddMessage(BriefTelegramMessage message, int? index = null);
    IChatMessageBuilderAddMessagesState AddUserMessage(string message);
    ChatMessage[] Build();
    bool CanAddMessage { get; }
    int TokensCount { get; }
}
