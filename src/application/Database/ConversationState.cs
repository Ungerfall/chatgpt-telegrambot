using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Ungerfall.ChatGpt.TelegramBot.Database;
public class ConversationState
{
    public string Id { get; set; } = null!;
    public long ChatId { get; set; }
    public int TopicId { get; set; }
    public long UserId { get; set; }
    public int BotMessageId { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public int RemainingCandidates { get; set; }
    public List<ConversationEntry> Context { get; set; } = [];
    public int Ttl { get; set; } = 86400;

    [JsonIgnore]
    public string? ETag { get; set; }
}

public class ConversationEntry
{
    public int MessageId { get; set; }
    public long UserId { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class ConversationReceipt
{
    public const string Processing = "processing";
    public const string Sending = "sending";
    public const string Completed = "completed";

    public string Id { get; set; } = null!;
    public long ChatId { get; set; }
    public string Stage { get; set; } = Processing;
    public string LeaseId { get; set; } = null!;
    public DateTime LeaseUntilUtc { get; set; }
    public int Ttl { get; set; } = 86400;

    [JsonIgnore]
    public string? ETag { get; set; }
}
