using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Ungerfall.ChatGpt.TelegramBot.Database;

namespace Ungerfall.ChatGpt.TelegramBot.Tests.Integration;

[Trait("Category", "Integration")]
public class CosmosDbTests
{
    [SkippableFact]
    public async Task TimedTasks_CreateAndExists_WithoutErrors()
    {
        Skip.IfNot(IntegrationTestsEnabled(), "integration tests are not enabled");

        var connectionString = Environment.GetEnvironmentVariable("CosmosDb", EnvironmentVariableTarget.Process)
            ?? throw new ArgumentException("CosmosDb is not set");
        var client = new CosmosClient(
            connectionString,
            clientOptions: new CosmosClientOptions
            {
                MaxRetryAttemptsOnRateLimitedRequests = 3,
                Serializer = new CosmosSystemTextJsonSerializer(),
            });
        const string containerId = "tests-timedTaskExecutions";
        const string databaseId = "telegram-bot";
        const string taskName = "quiz";
        DateTime date = DateTime.UtcNow;
        long chatId = Random.Shared.Next();
        var db = client.GetDatabase(databaseId);
        await db.CreateContainerIfNotExistsAsync(containerId, "/chatId");

        var repo = new TimedTaskExecutionRepository(
            client,
            Options.Create(new CosmosDbOptions
            {
                ConnectionString = connectionString,
                DatabaseId = databaseId,
                TimedTasksContainerId = containerId,
            }));
        if (!await repo.Exists(chatId, taskName, date, default))
        {
            await repo.Create(
                new TimedTaskExecution
                {
                    ChatId = chatId,
                    Name = taskName,
                    Type = "type",
                    DateUtc = date,
                    Id = Guid.NewGuid().ToString(),
                },
                default);
        }

        Assert.True(await repo.Exists(chatId, taskName, date, default));
    }

    [SkippableFact]
    public async Task TimedTasks_QuizLifeCycle_CompletesQuiz()
    {
        Skip.IfNot(IntegrationTestsEnabled(), "integration tests are not enabled");

        var connectionString = Environment.GetEnvironmentVariable("CosmosDb", EnvironmentVariableTarget.Process)
            ?? throw new ArgumentException("CosmosDb is not set");
        var client = new CosmosClient(
            connectionString,
            clientOptions: new CosmosClientOptions
            {
                MaxRetryAttemptsOnRateLimitedRequests = 3,
                Serializer = new CosmosSystemTextJsonSerializer(),
            });
        const string containerId = "tests-timedTaskExecutions";
        const string databaseId = "telegram-bot";
        long chatId = Random.Shared.Next();
        var db = client.GetDatabase(databaseId);
        await db.CreateContainerIfNotExistsAsync(containerId, "/chatId");

        var repo = new TimedTaskExecutionRepository(
            client,
            Options.Create(new CosmosDbOptions
            {
                ConnectionString = connectionString,
                DatabaseId = databaseId,
                TimedTasksContainerId = containerId,
            }));
        var id = Guid.NewGuid().ToString();
        var ingestedQuiz = new TimedTaskQuiz
        {
            ChatId = chatId,
            CorrectOptionId = 0,
            Options = ["1, 2, 3"],
            Question = "Which is 1",
            Type = TimedTaskQuiz.Type_ComputerScience,
            Id = id,
        };

        await repo.Create<TimedTaskQuiz>([ingestedQuiz], chatIdSelector: x => x.ChatId, cancellation: default);
        TimedTaskQuiz? quiz = await repo.GetQuiz(chatId, TimedTaskQuiz.Type_ComputerScience, cancellation: default);

        Assert.NotNull(quiz);
        Assert.Equal(ingestedQuiz.ChatId, quiz.ChatId);
        Assert.Equal(ingestedQuiz.CorrectOptionId, quiz.CorrectOptionId);
        Assert.True(ingestedQuiz.Options.SequenceEqual(quiz.Options));
        Assert.Equal(ingestedQuiz.Question, quiz.Question);
        Assert.Equal(ingestedQuiz.Type, quiz.Type);
        Assert.Equal(ingestedQuiz.Id, quiz.Id);

        await repo.CompleteQuiz(quiz, cancellation: default);

        TimedTaskQuiz? posted = await repo.GetQuiz(chatId, TimedTaskQuiz.Type_ComputerScience, cancellation: default);
        Assert.Null(posted);
    }

    public static bool IntegrationTestsEnabled()
    {
        return Environment.GetEnvironmentVariable("INTEGRATION_TESTS") == "1";
    }
}
