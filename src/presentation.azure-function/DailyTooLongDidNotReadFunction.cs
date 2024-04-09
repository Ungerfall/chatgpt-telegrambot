using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Ungerfall.ChatGpt.TelegramBot.AzureFunction;

public class DailyTooLongDidNotReadFunction
{
    private readonly TimedTasks.DailyTooLongDidNotRead _task;
    private readonly ILogger<DailyTooLongDidNotReadFunction> _logger;

    public DailyTooLongDidNotReadFunction(
        TimedTasks.DailyTooLongDidNotRead task,
        ILogger<DailyTooLongDidNotReadFunction> logger)
    {
        _task = task ?? throw new ArgumentNullException(nameof(task));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("DailyTooLongDidNotRead")]
    public async Task Run([TimerTrigger(TimedTasks.DailyTooLongDidNotRead.CRON_EXPRESSION)] TimerInfo timer)
    {
        _logger.LogInformation("C# Timer trigger function executed at: {now}", DateTime.Now);

        await _task.Execute();

        _logger.LogInformation("Next timer schedule at: {nextSchedule}", timer?.ScheduleStatus?.Next);
    }
}
