using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OpenAI.Interfaces;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using System;
using System.Threading;
using System.Threading.Tasks;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using Ungerfall.ChatGpt.TelegramBot.Database;

namespace Ungerfall.ChatGpt.TelegramBot.AzureFunction;

public class DailyTooLongDidNotReadTodayFunction
{
    private readonly ILogger _logger;

    public DailyTooLongDidNotReadTodayFunction(ILogger<DailyTooLongDidNotReadTodayFunction> logger)
    {
        _logger = logger;
    }

    [Function("DailyTooLongDidNotReadToday")]
    public async Task Run([TimerTrigger(TimedTasks.DailyTooLongDidNotReadToday.CRONTAB)] TimerInfo timer)
    {
        _logger.LogInformation("C# Timer trigger function executed at: {now}", DateTime.Now);

        _logger.LogInformation("Next timer schedule at: {nextSchedule}", timer?.ScheduleStatus?.Next);
    }
}
