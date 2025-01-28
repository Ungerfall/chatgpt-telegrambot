using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Telegram.Bot;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;

namespace Ungerfall.ChatGpt.TelegramBot.TimedTasks;
public abstract class TimedTask
{
    protected readonly ITelegramBotClient _botClient;
    protected readonly ITimedTaskExecutionRepository _repo;

    private readonly ILogger _logger;

    internal TimedTask(ITimedTaskExecutionRepository repo, ITelegramBotClient botClient, ILogger logger)
    {
        _botClient = botClient;
        _repo = repo;
        _logger = logger;
    }

    public async Task Execute([CallerMemberName] string? caller = default)
    {
        foreach (var chatId in ChatIds)
        {
            try
            {
                if (await _repo.Exists(chatId, Name, Date, cancellation: default))
                {
                    _logger.LogInformation("Skipping {Caller} as already executed for ({ChatId}, {Name}, {ExecutionDate})",
                        caller, chatId, Name, Date);
                    continue;
                }

                await ExecuteForChat(chatId);
                await _repo.Create(
                    new Database.TimedTaskExecution
                    {
                        Id = Guid.NewGuid().ToString(),
                        ChatId = chatId,
                        DateUtc = Date,
                        Name = Name,
                        Type = Type,
                    },
                    cancellation: default);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Timed Task ({Name}, {ChatId}) error", Name, chatId);
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: $"Ошибка выполнения запланированного события ({Name})");
            }
        }
    }

    protected abstract IEnumerable<long> ChatIds { get; }
    protected abstract string Name { get; }
    protected abstract DateTime Date { get; }
    protected abstract string Type { get; }

    protected abstract Task ExecuteForChat(long chatId);
}
