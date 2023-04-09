using System;
using System.Text.Json.Serialization;

namespace Ungerfall.ChatGpt.TelegramBot.Database;

public class BriefTelegramMessage
{
    public const string DATE_UTC_FORMAT = "yyyy-MM-dd";

    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("user")]
    public string User { get; init; } = null!;

    [JsonPropertyName("userId")]
    public long UserId { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = null!;

    [JsonPropertyName("messageId")]
    public int MessageId { get; init; }

    [JsonPropertyName("date")]
    public DateTime Date { get; init; }

    [JsonPropertyName("dateUtc")]
    public string DateUtc { get; init; } = null!;

    [JsonPropertyName("ttl")]
    public int TTL { get; init; }
}
