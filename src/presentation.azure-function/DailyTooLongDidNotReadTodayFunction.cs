using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Ungerfall.ChatGpt.TelegramBot.AzureFunction;

public class DailyTooLongDidNotReadTodayFunction
{
    private readonly TimedTasks.DailyTooLongDidNotReadToday _task;
    private readonly ILogger<DailyTooLongDidNotReadTodayFunction> _logger;

    public DailyTooLongDidNotReadTodayFunction(
        TimedTasks.DailyTooLongDidNotReadToday task,
        ILogger<DailyTooLongDidNotReadTodayFunction> logger)
    {
        _task = task ?? throw new ArgumentNullException(nameof(task));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("DailyTooLongDidNotReadToday")]
    public async Task Run([TimerTrigger(TimedTasks.DailyTooLongDidNotReadToday.CRON_EXPRESSION)] TimerInfo timer)
    {
        _logger.LogInformation("C# Timer trigger function executed at: {now}", DateTime.Now);

        await _task.Execute();

        _logger.LogInformation("Next timer schedule at: {nextSchedule}", timer?.ScheduleStatus?.Next);
    }
}
