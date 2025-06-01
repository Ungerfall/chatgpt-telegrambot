using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Ungerfall.ChatGpt.TelegramBot.AzureFunction;
public class DailyVideoGamesQuizFunction
{
    private readonly TimedTasks.DailyVideoGamesQuiz _task;
    private readonly ILogger<DailyVideoGamesQuizFunction> _logger;

    public DailyVideoGamesQuizFunction(
        TimedTasks.DailyVideoGamesQuiz task,
        ILogger<DailyVideoGamesQuizFunction> logger)
    {
        _task = task ?? throw new ArgumentNullException(nameof(task));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("DailyVideoGamesQuiz")]
    public async Task Run([TimerTrigger(TimedTasks.DailyVideoGamesQuiz.CRON_EXPRESSION)] TimerInfo timer)
    {
        _logger.LogInformation("C# Timer trigger for {Task} function executed at: {Now}", _task.GetType(), DateTime.Now);

        await Task.CompletedTask;
        // TODO: ingest quizzes before removing

        _logger.LogInformation("Next timer schedule at: {NextSchedule}", timer?.ScheduleStatus?.Next);
    }
}
