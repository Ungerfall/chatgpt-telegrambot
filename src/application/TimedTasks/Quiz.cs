using System.Text.Json.Serialization;

namespace Ungerfall.ChatGpt.TelegramBot.TimedTasks;
public class Quiz
{
    [JsonPropertyName("question")]
    public required string Question { get; set; }

    [JsonPropertyName("options")]
    public required string[] Options { get; set; }

    [JsonPropertyName("correctOptionId")]
    public required int CorrectOptionId { get; set; }

    [JsonPropertyName("explanation")]
    public string? Explanation { get; set; }
}
