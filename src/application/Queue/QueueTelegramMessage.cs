using System;
using System.Text.Json.Serialization;

namespace Ungerfall.ChatGpt.TelegramBot.Queue;

public class QueueTelegramMessage
{
    public const string QUEUE_NAME = "tgbot-messages";

    [JsonPropertyName("chatId")]
    public long ChatId { get; init; }

    [JsonPropertyName("user")]
    public string User { get; init; } = null!;

    [JsonPropertyName("userId")]
    public long UserId { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = null!;

    [JsonPropertyName("messageId")]
    public int MessageId { get; init; }

    [JsonPropertyName("date")]
    public DateTime Date { get; init; }
}
