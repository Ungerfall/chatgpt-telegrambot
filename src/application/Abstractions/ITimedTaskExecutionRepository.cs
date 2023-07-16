using System;
using System.Threading;
using System.Threading.Tasks;
using Ungerfall.ChatGpt.TelegramBot.Database;

namespace Ungerfall.ChatGpt.TelegramBot.Abstractions;
public interface ITimedTaskExecutionRepository
{
    Task Create(TimedTaskExecution timedTask, CancellationToken cancellation);
    Task<bool> Exists(string name, DateTime date, CancellationToken cancellation);
}
