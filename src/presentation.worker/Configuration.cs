using System.Diagnostics.CodeAnalysis;

namespace Ungerfall.ChatGpt.TelegramBot.Worker;

public class Configuration
{
    [DisallowNull]
    public string TelegramBotToken { get; set; } = null!;

    [DisallowNull]
    public string OpenAiOrg { get; set; } = null!;

    [DisallowNull]
    public string OpenAiApiKey { get; set; } = null!;
}
