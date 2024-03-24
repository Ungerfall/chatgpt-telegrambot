using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Ungerfall.ChatGpt.TelegramBot.AzureFunction;
public class DailyQuizFunction
{
    private readonly TimedTasks.DailyQuiz _task;
    private readonly ILogger<DailyQuizFunction> _logger;

    public DailyQuizFunction(
        TimedTasks.DailyQuiz task,
        ILogger<DailyQuizFunction> logger)
    {
        _task = task ?? throw new ArgumentNullException(nameof(task));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("DailyQuiz")]
    public async Task Run([TimerTrigger(TimedTasks.DailyQuiz.CRON_EXPRESSION)] TimerInfo timer)
    {
        _logger.LogInformation("C# Timer trigger function executed at: {now}", DateTime.Now);

        await _task.Execute();

        _logger.LogInformation("Next timer schedule at: {nextSchedule}", timer?.ScheduleStatus?.Next);
    }

}
