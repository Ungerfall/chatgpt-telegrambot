using Microsoft.Extensions.Logging;
using NSubstitute;
using Telegram.Bot;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using Ungerfall.ChatGpt.TelegramBot.Configuration;
using Ungerfall.ChatGpt.TelegramBot.TimedTasks;

namespace Ungerfall.ChatGpt.TelegramBot.Tests.Integration;
[Trait("Category", "Integration")]
public class DailyQuizTests
{
    /*
        repo is mocked as tested in the another test
        env variables:
        1. QuizChats
        2. IntegrationTests_TELEGRAM_BOT_TOKEN 
    */
    [SkippableFact]
    public async Task DailyQuiz_RequestsQuiz_SendsToTestUser()
    {
        Skip.IfNot(CosmosDbTests.IntegrationTestsEnabled(), "integration tests are not enabled");

        var quizChats = new QuizChats();
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Trace));
        var logger = loggerFactory.CreateLogger<DailyQuiz>();
        var repo = Substitute.For<ITimedTaskExecutionRepository>();
        repo.Exists(default, string.Empty, default, default)
            .ReturnsForAnyArgs(false);
        repo.Create(new Database.TimedTaskExecution { Name = "some", Type = "required" }, default)
            .ReturnsForAnyArgs(Task.CompletedTask);
        repo.GetQuiz(default, Database.TimedTaskQuiz.Type_ComputerScience, default)
            .ReturnsForAnyArgs(new Database.TimedTaskQuiz
            {
                ChatId = 123,
                CorrectOptionId = 1,
                Options = ["0", "1", "2"],
                Question = "Which is 1",
                Type = "some-type",
                Explanation = "cool",
                Id = Guid.NewGuid().ToString(),
            });
        repo.CompleteQuiz(Arg.Any<Database.TimedTaskQuiz>(), default)
            .ReturnsForAnyArgs(Task.CompletedTask);
        var tgToken = Environment.GetEnvironmentVariable("IntegrationTests_TELEGRAM_BOT_TOKEN", EnvironmentVariableTarget.Process)
            ?? throw new ArgumentException("IntegrationTests_TELEGRAM_BOT_TOKEN is missing");
        var botClient = new TelegramBotClient(tgToken);

        var dailyQuiz = new DailyQuiz(quizChats, botClient, repo, logger);

        await dailyQuiz.Execute();
    }
}
