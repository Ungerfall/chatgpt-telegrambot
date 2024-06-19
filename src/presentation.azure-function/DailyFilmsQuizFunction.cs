using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Ungerfall.ChatGpt.TelegramBot.AzureFunction;
public class DailyFilmsQuizFunction
{
    private readonly TimedTasks.DailyFilmsQuiz _task;
    private readonly ILogger<DailyFilmsQuizFunction> _logger;

    public DailyFilmsQuizFunction(
        TimedTasks.DailyFilmsQuiz task,
        ILogger<DailyFilmsQuizFunction> logger)
    {
        _task = task ?? throw new ArgumentNullException(nameof(task));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("DailyFilmsQuiz")]
    public async Task Run([TimerTrigger(TimedTasks.DailyFilmsQuiz.CRON_EXPRESSION)] TimerInfo timer)
    {
        _logger.LogInformation("C# Timer trigger function executed at: {now}", DateTime.Now);

        await _task.Execute();

        _logger.LogInformation("Next timer schedule at: {nextSchedule}", timer?.ScheduleStatus?.Next);
    }
}
