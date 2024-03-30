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

    private static bool IntegrationTestsEnabled()
    {
        return Environment.GetEnvironmentVariable("INTEGRATION_TESTS") == "1";
    }
}
