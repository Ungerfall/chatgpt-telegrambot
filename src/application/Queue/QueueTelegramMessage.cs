using System.Text.Json.Serialization;

namespace Ungerfall.ChatGpt.TelegramBot.Queue;

public class QueueTelegramMessage
{
    [JsonPropertyName("user")]
    public string User { get; init; } = null!;

    [JsonPropertyName("message")]
    public string Message { get; init; } = null!;
}
