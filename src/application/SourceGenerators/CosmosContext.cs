using System.Text.Json.Serialization;

namespace Ungerfall.ChatGpt.TelegramBot.SourceGenerators;
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Database.TelegramMessage))]
[JsonSerializable(typeof(Database.TelegramMessage[]))]
[JsonSerializable(typeof(Database.TimedTaskExecution))]
[JsonSerializable(typeof(Database.TimedTaskExecution[]))]
[JsonSerializable(typeof(int[]))]
internal partial class CosmosContext : JsonSerializerContext;
