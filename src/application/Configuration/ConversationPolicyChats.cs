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
        if (options.WindowSeconds is < 0 or > 120 || options.MaxCandidates is < 0 or > 3
            || !double.IsFinite(options.AutonomousProbability) || options.AutonomousProbability is < 0 or > .01
            || options.CooldownMinutes is < 30 or > 1440 || options.AutonomousDailyLimit is < 0 or > 3
            || options.DecisionDailyLimit is < 0 or > 100
            || !double.IsFinite(options.MinProbability) || options.MinProbability is < .95 or > 1)
        {
            throw new ArgumentException("Conversation policy limits are outside the allowed ranges.");
        }
        return options;
    }
}

public class ConversationPolicyOptions
{
    public bool TrackContinuation { get; set; }
    public bool DecisionsEnabled { get; set; }
    public bool ContinuationEnabled { get; set; }
    public bool AutonomousEnabled { get; set; }
    public int WindowSeconds { get; set; } = 60;
    public int MaxCandidates { get; set; } = 2;
    public double AutonomousProbability { get; set; } = .001;
    public int CooldownMinutes { get; set; } = 120;
    public int AutonomousDailyLimit { get; set; } = 1;
    public int DecisionDailyLimit { get; set; } = 20;
    public double MinProbability { get; set; } = .95;
}
