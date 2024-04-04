using System;
using System.Threading;
using System.Threading.Tasks;
using Ungerfall.ChatGpt.TelegramBot.Database;

namespace Ungerfall.ChatGpt.TelegramBot.Abstractions;
public interface ITimedTaskExecutionRepository
{
    Task Create(TimedTaskExecution timedTask, CancellationToken cancellation);
    Task<bool> Exists(long chatId, string name, DateTime date, CancellationToken cancellation);
    Task<TimedTaskQuiz?> GetQuiz(long chatId, string type, CancellationToken cancellation);
    Task CompleteQuiz(TimedTaskQuiz quiz, CancellationToken cancellation);
}
