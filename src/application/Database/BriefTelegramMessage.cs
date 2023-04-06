using System;
using System.Text.Json.Serialization;

namespace Ungerfall.ChatGpt.TelegramBot.Database;

public class BriefTelegramMessage
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("user")]
    public string User { get; init; } = null!;

    [JsonPropertyName("message")]
    public string Message { get; init; } = null!;

    [JsonPropertyName("dateUtc")]
    public string DateUtc { get; init; } = null!;

    [JsonPropertyName("ttl")]
    public int TTL { get; init; }
}
