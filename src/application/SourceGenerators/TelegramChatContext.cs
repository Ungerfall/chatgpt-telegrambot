using System.Text.Json.Serialization;

namespace Ungerfall.ChatGpt.TelegramBot.SourceGenerators;
[JsonSourceGenerationOptions(WriteIndented = true, MaxDepth = 3)]
[JsonSerializable(typeof(Telegram.Bot.Types.Chat))]
internal partial class TelegramChatContext : JsonSerializerContext;
