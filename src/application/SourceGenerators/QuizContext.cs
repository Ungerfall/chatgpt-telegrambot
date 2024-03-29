using System.Text.Json.Serialization;
using Ungerfall.ChatGpt.TelegramBot.TimedTasks;

namespace Ungerfall.ChatGpt.TelegramBot.SourceGenerators;
[JsonSerializable(typeof(Quiz))]
internal partial class QuizContext : JsonSerializerContext;
