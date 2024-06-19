using System;
using System.Text.Json.Serialization;

namespace Ungerfall.ChatGpt.TelegramBot.Database;
public class TimedTaskQuiz
{
    public const string Type_ComputerScience = "quiz-type-cs";
    public const string Type_Films = "quiz-type-films";

    [JsonPropertyName("id")]
    public string Id { get; init; } = null!;
    [JsonPropertyName("chatId")]
    public required long ChatId { get; set; }
    [JsonPropertyName("type")]
    public required string Type { get; set; }
    [JsonPropertyName("question")]
    public required string Question { get; set; }
    [JsonPropertyName("options")]
    public required string[] Options { get; set; }
    [JsonPropertyName("correctOptionId")]
    public required int CorrectOptionId { get; set; }
    [JsonPropertyName("explanation")]
    public string? Explanation { get; init; }
    [JsonPropertyName("posted")]
    public bool Posted { get; set; }
    [JsonPropertyName("date")]
    public DateTime? DateUtc { get; set; }
}
