using DeepL;
using DeepL.Model;
using Microsoft.Azure.Cosmos;
using ShellProgressBar;
using System.CommandLine;
using System.Text;
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
    var cosmosDbConn = MaskInput("Enter your cosmos db connection string");
    var deepLAuth = MaskInput("Enter your DeepL API key");
    Console.WriteLine("Enter comma-separated chat ids:");
    long[] chats = (Console.ReadLine() ?? string.Empty).Split(',').Select(long.Parse).ToArray();

    // init services
    var cosmosClient = new CosmosClient(
        cosmosDbConn,
        clientOptions: new CosmosClientOptions
        {
            MaxRetryAttemptsOnRateLimitedRequests = 3,
            Serializer = new CosmosSystemTextJsonSerializer(),
        });
    var translator = new Translator(deepLAuth);

    // get open trivia response
    HttpClient httpClient = new();
    Console.WriteLine("Getting {0} quizzes from Open Trivia DB...", amount);
    var res = await httpClient.GetAsync($"https://opentdb.com/api.php?amount={amount}&category=11&type=multiple&encode=base64");
    string content = await res.Content.ReadAsStringAsync();
    OpenTriviaResponse response = JsonSerializer.Deserialize(
        content,
        typeof(OpenTriviaResponse),
        QuizContext.Default) as OpenTriviaResponse
        ?? throw new ArgumentException($"Cannot deserialize HTTP response {content}");
    // translate
    var options = new ProgressBarOptions { ProgressCharacter = '*' };
    using (var progress = new ProgressBar(response.Quizzes.Length, "Translate to Russian using DeepL", options))
    {
        foreach (var quiz in response.Quizzes)
        {
            progress.Tick();
            TextResult translatedQuestion = await translator.TranslateTextAsync(
                  FromBase64(quiz.Question),
                  LanguageCode.English,
                  LanguageCode.Russian,
                  new TextTranslateOptions { PreserveFormatting = true });
            quiz.Question = translatedQuestion.Text;
            for (int i = 0; i < quiz.IncorrectAnswers.Length; i++)
            {
                TextResult translatedIncorrectAnswer = await translator.TranslateTextAsync(
                    FromBase64(quiz.IncorrectAnswers[i]),
                    LanguageCode.English,
                    LanguageCode.Russian,
                    new TextTranslateOptions { PreserveFormatting = true });
                quiz.IncorrectAnswers[i] = translatedIncorrectAnswer.Text;
            }

            TextResult translatedCorrectAnswer = await translator.TranslateTextAsync(
                FromBase64(quiz.CorrectionAnswer),
                LanguageCode.English,
                LanguageCode.Russian,
                new TextTranslateOptions { PreserveFormatting = true });
            quiz.CorrectionAnswer = translatedCorrectAnswer.Text;
        }
    }

    TimedTaskQuiz[] batch = [.. response.Quizzes
        .SelectMany(q =>
        {
            Dictionary<string, int> indexedOptions = q.IncorrectAnswers
                .Concat([q.CorrectionAnswer])
                .Distinct()
                .OrderBy(_ => Random.Shared.Next())
                .Select((option, i) => (option, i))
                .ToDictionary();
            int correctOptionId = indexedOptions[q.CorrectionAnswer];

            return chats
                .Select(id => new TimedTaskQuiz
                {
                    ChatId = id,
                    CorrectOptionId = correctOptionId,
                    Options = [.. indexedOptions.Keys],
                    Question = q.Question,
                    Type = TimedTaskQuiz.Type_Films,
                    Id = Guid.NewGuid().ToString(),
                    DateUtc = DateTime.UtcNow,
                });
        })];

    var repo = new TimedTaskExecutionRepository(
        cosmosClient,
        Microsoft.Extensions.Options.Options.Create(new CosmosDbOptions
        {
            ConnectionString = cosmosDbConn,
            DatabaseId = "telegram-bot",
            TimedTasksContainerId = "timedTaskExecutions"
        }));
    Console.WriteLine("Saving quizzes to Cosmos DB container...");
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

static string FromBase64(string base64)
{
    byte[] data = Convert.FromBase64String(base64);
    return Encoding.UTF8.GetString(data);
}

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
