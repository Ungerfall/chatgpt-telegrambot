using DeepL;
using DeepL.Model;
using Microsoft.Azure.Cosmos;
using ShellProgressBar;
using System.CommandLine;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ungerfall.ChatGpt.TelegramBot;
using Ungerfall.ChatGpt.TelegramBot.Database;

var numberOfQuestions = new Option<int?>(
    aliases: ["--number", "-n"],
    description: """
        Number of questions
    """);
var category = new Option<int?>(
    aliases: ["--category", "-c"],
    description: """
    Quizzes category:
    9: General Knowledge
    10: Entertainment: Books
    11: Entertainment: Film
    12: Entertainment: Music
    13: Entertainment: Musicals & Theatres
    14: Entertainment: Television
    15: Entertainment: Video Games
    16: Entertainment: Board Games
    17: Science & Nature
    18: Science: Computers
    19: Science: Mathematics
    20: Mythology
    21: Sports
    22: Geography
    23: History
    24: Politics
    25: Art
    26: Celebrities
    27: Animals
    28: Vehicles
    29: Entertainment: Comics
    30: Science: Gadgets
    31: Entertainment: Japanese Anime & Manga
    32: Entertainment: Cartoon & Animations
    """);
var quizTypes = new Dictionary<int, string>
{
    [11] = TimedTaskQuiz.Type_Films,
    [15] = TimedTaskQuiz.Type_VideoGames,
};
var rootCommand = new RootCommand("Quiz ingestion from Open Trivia DB");
rootCommand.AddOption(numberOfQuestions);
rootCommand.AddOption(category);
rootCommand.SetHandler(async (int? amount, int? category) =>
{
    if (amount is null)
    {
        return;
    }

    if (category is null)
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
    var res = await httpClient.GetAsync($"https://opentdb.com/api.php?amount={amount}&category={category}&type=multiple&encode=base64");
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

    string quizType = quizTypes[category.Value];
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

            var (hash, algo) = ComputeHash(q);
            return chats
                .Select(id => new TimedTaskQuiz
                {
                    ChatId = id,
                    CorrectOptionId = correctOptionId,
                    Options = [.. indexedOptions.Keys],
                    Question = q.Question,
                    Type = quizType,
                    Id = Guid.NewGuid().ToString(),
                    DateUtc = DateTime.UtcNow,
                    ComputedHash = hash,
                    HashAlgorithm = algo,
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
}, numberOfQuestions
, category);

await rootCommand.InvokeAsync(args);

static string FromBase64(string base64)
{
    byte[] data = Convert.FromBase64String(base64);
    return Encoding.UTF8.GetString(data);
}

static (string Hash, string Algo) ComputeHash(QuizSource s)
{
    byte[] data = Encoding.UTF8.GetBytes(string.Concat(s.Question, s.Type));
    data = SHA256.HashData(data);
    StringBuilder sBuilder = new();
    for (int i = 0; i < data.Length; i++)
    {
        sBuilder.Append(data[i].ToString("x2"));
    }

    return (
        sBuilder.ToString(),
        HashAlgorithmName.SHA256.Name
            ?? throw new InvalidOperationException("HashAlgorithm does not have name"));
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
