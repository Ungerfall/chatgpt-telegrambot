using System;
using System.ComponentModel.DataAnnotations;

namespace Ungerfall.ChatGpt.TelegramBot.Configuration;
public class ChatGptPrompt
{
    /// <summary>
    /// Number of tokens for historical messages in a request.
    /// </summary>
    [Required]
    [Range(1d, 1024 * 4)]
    public int Tokens { get; set; }

    [Required]
    public string Prompt { get; set; } = null!;
}
