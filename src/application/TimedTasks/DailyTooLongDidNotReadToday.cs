using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ungerfall.ChatGpt.TelegramBot.TimedTasks;
public class DailyTooLongDidNotReadToday
{
    public const string CRON_EXPRESSION = "0 30 23 * * *";

    public async Task Execute()
    {
        await Task.CompletedTask;
    }
}
