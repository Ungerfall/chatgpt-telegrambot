using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ungerfall.ChatGpt.TelegramBot.TimedTasks;
public class DailyTooLongDidNotReadToday
{
    public const string CRONTAB = "0 30 23 * * *";

    public Task Execute()
    {
        return Task.CompletedTask;
    }
}
