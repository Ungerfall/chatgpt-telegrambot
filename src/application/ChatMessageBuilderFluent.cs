﻿using OpenAI.ObjectModels.RequestModels;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using Ungerfall.ChatGpt.TelegramBot.Database;

namespace Ungerfall.ChatGpt.TelegramBot;
public interface IChatMessageBuilderTokenCounterState
{
    IChatMessageBuilderSystemRoleState WithTokenCounter(ITokenCounter? counter = null);
}
public interface IChatMessageBuilderSystemRoleState
{
    IChatMessageBuilderAddMessagesState WithSystemRoleMessage(string msg);
}
public interface IChatMessageBuilderAddMessagesState
{
    IChatMessageBuilderAddMessagesState AddMessage(TelegramMessage message, int? index = null);
    IChatMessageBuilderAddMessagesState AddUserMessage(string message);
    ChatMessage[] Build();
    bool CanAddMessage { get; }
    int TokensCount { get; }
    bool ContainsUserMessage { get; }
}
