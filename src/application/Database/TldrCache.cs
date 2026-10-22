using System;
using System.Text.Json.Serialization;

namespace Ungerfall.ChatGpt.TelegramBot.Database;
public class TldrCache
{
    public string Id { get; set; } = null!;
    public long ChatId { get; set; }
    public DateTime LeaseUntilUtc { get; set; }
    public string? Summary { get; set; }
    public int Ttl { get; set; } = 86400;

    [JsonIgnore]
    public string? ETag { get; set; }
}
