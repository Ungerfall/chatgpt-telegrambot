using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Ungerfall.ChatGpt.TelegramBot.Configuration;
public class ConversationPolicyChats
{
    public ConversationPolicyOptions Get(long chatId)
    {
        var json = Environment.GetEnvironmentVariable("ConversationPolicyChats", EnvironmentVariableTarget.Process);
        var options = string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<Dictionary<long, ConversationPolicyOptions>>(json)?.GetValueOrDefault(chatId);
        options ??= new ConversationPolicyOptions();
        if (options.WindowSeconds is < 0 or > 120 || options.MaxCandidates is < 0 or > 3)
        {
            throw new ArgumentException("Conversation window must be 0-120 seconds and contain at most 3 candidates.");
        }
        return options;
    }
}

public class ConversationPolicyOptions
{
    public bool TrackContinuation { get; set; }
    public int WindowSeconds { get; set; } = 60;
    public int MaxCandidates { get; set; } = 2;
}
