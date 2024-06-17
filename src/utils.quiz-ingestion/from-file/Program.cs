using Microsoft.Azure.Cosmos;
using OpenAI.Managers;
using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ungerfall.ChatGpt.TelegramBot;
using Ungerfall.ChatGpt.TelegramBot.Database;

var fileOption = new Option<FileInfo?>(
    aliases: ["--file", "-f"],
    description: """
    JSON file that contains array of objects with schema:
    {
      "$schema": "https://json-schema.org/draft/2020-12/schema",
      "$id": "https://example.com/product.schema.json",
      "title": "Quiz",
      "description": "IT and Computer Science quiz",
      "type": "object",
      "properties": {
        "question": {
          "description": "Quiz question",
          "type": "string",
          "minLength": 1,
          "maxLength": 300
        },
        "options": {
          "description": "From 4 to 8 options",
          "type": "array",
          "minItems": 4,
          "maxItems": 8,
          "uniqueItems": true,
          "items": {
            "type": "string"
          }
        },
        "correctOptionId": {
          "description": "0-based identifier of correct option",
          "type": "integer",
          "minimum": 0
        },
        "explanation": {
          "description": "Short correct answer explanation",
          "type": "string",
          "maxLength": 100
        }
      }
    }
    """);
var rootCommand = new RootCommand("Quiz ingestion");
rootCommand.AddOption(fileOption);
rootCommand.SetHandler(async file =>
{
    if (file is null)
    {
        return;
    }

    var openAiApiKey = MaskInput("Enter your Open AI API key");
    var cosmosDbConn = MaskInput("Enter your cosmos db connection string");
    Console.WriteLine("Enter comma-separated chat ids:");
    long[] chats = (Console.ReadLine() ?? string.Empty).Split(',').Select(long.Parse).ToArray();
    var openAiService = new OpenAIService(new OpenAI.OpenAiOptions
    {
        ApiKey = openAiApiKey,
        DefaultModelId = OpenAI.ObjectModels.Models.Gpt_3_5_Turbo,
    });
    var cosmosClient = new CosmosClient(
        cosmosDbConn,
        clientOptions: new CosmosClientOptions
        {
            MaxRetryAttemptsOnRateLimitedRequests = 3,
            Serializer = new CosmosSystemTextJsonSerializer(),
        });
    using var sr = file.OpenRead();
    QuizSource[] quizzes = JsonSerializer.Deserialize(sr, typeof(QuizSource[]), QuizContext.Default) as QuizSource[]
        ?? throw new ArgumentException("Cannot deserialize file");
    var repo = new TimedTaskExecutionRepository(
        cosmosClient,
        Microsoft.Extensions.Options.Options.Create(new CosmosDbOptions
        {
            ConnectionString = cosmosDbConn,
            DatabaseId = "telegram-bot",
            TimedTasksContainerId = "timedTaskExecutions"
        }));
    var batch = quizzes
     .SelectMany(_ => chats, (r, c) => (r, c))
     .Select(item =>
     {
         var (quiz, chatId) = item;
         return new TimedTaskQuiz
         {
             ChatId = chatId,
             CorrectOptionId = quiz.CorrectOptionId,
             Options = quiz.Options,
             Question = quiz.Question,
             Type = TimedTaskQuiz.Type_ComputerScience,
             Explanation = quiz.Explanation,
             Id = Guid.NewGuid().ToString(),
             DateUtc = DateTime.UtcNow,
         };
     });

    await repo.Create(batch, chatIdSelector: item => item.ChatId, default);

    static string MaskInput(string intro)
    {
        Console.Write($"{intro}: ");
        string password = "";
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter)
            {
                break;
            }
            else
            {
                password += key.KeyChar;
                Console.Write("*");
            }
        }

        Console.WriteLine();
        return password;
    }
}, fileOption);

await rootCommand.InvokeAsync(args);

internal sealed class QuizSource
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

[JsonSerializable(typeof(QuizSource))]
[JsonSerializable(typeof(QuizSource[]))]
internal partial class QuizContext : JsonSerializerContext;