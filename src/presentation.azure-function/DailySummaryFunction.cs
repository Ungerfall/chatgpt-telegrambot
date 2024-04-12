using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Ungerfall.ChatGpt.TelegramBot.AzureFunction;

public class DailySummaryFunction(
    TimedTasks.DailySummary task,
    ILogger<DailySummaryFunction> logger)
{
    private readonly TimedTasks.DailySummary _task = task ?? throw new ArgumentNullException(nameof(task));
    private readonly ILogger<DailySummaryFunction> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    [Function("DailySummary")]
    public async Task Run([TimerTrigger(TimedTasks.DailySummary.CRON_EXPRESSION)] TimerInfo timer)
    {
        _logger.LogInformation("C# Timer trigger function executed at: {now}", DateTime.Now);

        await _task.Execute();

        _logger.LogInformation("Next timer schedule at: {nextSchedule}", timer?.ScheduleStatus?.Next);
    }
}
