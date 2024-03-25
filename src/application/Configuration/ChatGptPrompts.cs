using System.ComponentModel.DataAnnotations;

namespace Ungerfall.ChatGpt.TelegramBot.Configuration;
public class ChatGptPrompts
{
    public const string Section = "ChatGptPrompts";

    [Required]
    public required ChatGptPrompt TlDrPrompt { get; set; }
}
