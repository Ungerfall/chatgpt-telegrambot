using Microsoft.Azure.Cosmos;
using OpenAI.Managers;
using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ungerfall.ChatGpt.TelegramBot;
using Ungerfall.ChatGpt.TelegramBot.Database;

var numberOfQuestions = new Option<int?>(
    aliases: ["--number", "-n"],
    description: """
        Number of questions to ingest
    """);
var rootCommand = new RootCommand("Quiz ingestion from Open Trivia DB");
rootCommand.AddOption(numberOfQuestions);
rootCommand.SetHandler(async (int? amount) =>
{
    if (amount is null)
    {
        return;
    }

    // interactive mode
    var openAiApiKey = MaskInput("Enter your Open AI API key");
    var cosmosDbConn = MaskInput("Enter your cosmos db connection string");
    Console.WriteLine("Enter comma-separated chat ids:");
    long[] chats = (Console.ReadLine() ?? string.Empty).Split(',').Select(long.Parse).ToArray();

    // init services
    var openAiService = new OpenAIService(new OpenAI.OpenAiOptions
    {
        ApiKey = openAiApiKey,
        DefaultModelId = OpenAI.ObjectModels.Models.Gpt_4,
    });
    var cosmosClient = new CosmosClient(
        cosmosDbConn,
        clientOptions: new CosmosClientOptions
        {
            MaxRetryAttemptsOnRateLimitedRequests = 3,
            Serializer = new CosmosSystemTextJsonSerializer(),
        });

    // get open trivia quizzes
    HttpClient httpClient = new();
    var res = await httpClient.GetAsync($"https://opentdb.com/api.php?amount={amount}&category=11&type=multiple");
    QuizSource[] quizzes = JsonSerializer.Deserialize(
        await res.Content.ReadAsStringAsync(),
        typeof(QuizSource[]),
        QuizContext.Default) as QuizSource[]
        ?? throw new ArgumentException("Cannot deserialize amount");
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
         (QuizSource quiz, long chatId) = item;
         Dictionary<string, int> options = quiz.IncorrectAnswers
            .Concat([quiz.CorrectionAnswer])
            .Select((option, i) => (option, i)) // index options [0..Length)
            .ToDictionary(keySelector: x => x.option, elementSelector: x => x.i);
         int correctOptionId = options[quiz.CorrectionAnswer];

         return new TimedTaskQuiz
         {
             ChatId = chatId,
             CorrectOptionId = correctOptionId,
             Options = [.. options.Keys],
             Question = quiz.Question,
             Type = TimedTaskQuiz.Type_Films,
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
}, numberOfQuestions);

await rootCommand.InvokeAsync(args);

internal sealed class OpenTriviaResponse
{
    [JsonPropertyName("response_code")]
    public required int ResponseCode { get; set; }
    [JsonPropertyName("results")]
    public required QuizSource[] Quizzes { get; set; }
}
internal sealed class QuizSource
{
    /*
    "type": "multiple",
      "difficulty": "easy",
      "category": "Entertainment: Film",
      "question": "Who directed the Kill Bill movies?",
      "correct_answer": "Quentin Tarantino",
      "incorrect_answers": [
        "Arnold Schwarzenegger",
        "David Lean",
        "Stanley Kubrick"
      ]
     */
    [JsonPropertyName("type")]
    public required string Type { get; set; }
    [JsonPropertyName("difficulty")]
    public required string Difficulty { get; set; }
    [JsonPropertyName("question")]
    public required string Question { get; set; }
    [JsonPropertyName("correct_answer")]
    public required string CorrectionAnswer { get; set; }
    [JsonPropertyName("incorrect_answers")]
    public required string[] IncorrectAnswers { get; set; }
}

[JsonSerializable(typeof(OpenTriviaResponse))]
[JsonSerializable(typeof(QuizSource))]
[JsonSerializable(typeof(QuizSource[]))]
internal partial class QuizContext : JsonSerializerContext;
