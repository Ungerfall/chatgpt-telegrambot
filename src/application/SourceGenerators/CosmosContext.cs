using System.Text.Json.Serialization;

namespace Ungerfall.ChatGpt.TelegramBot.SourceGenerators;
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Database.TelegramMessage))]
[JsonSerializable(typeof(Database.TimedTaskExecution))]
internal partial class CosmosContext : JsonSerializerContext;
