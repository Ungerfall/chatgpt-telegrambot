using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Bot;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using Ungerfall.ChatGpt.TelegramBot.Configuration;

namespace Ungerfall.ChatGpt.TelegramBot.TimedTasks;
/// <summary>
/// Executed /tl;dr command daily
/// </summary>
public class DailyTooLongDidNotRead : TimedTask
{
    public const string CRON_EXPRESSION = "0 55 23 * * *";

    private readonly Commands.TooLongDidNotReadToday _command;
    private readonly DailyTooLongDidNotReadChats _chats;

    public DailyTooLongDidNotRead(
        Commands.TooLongDidNotReadToday command,
        DailyTooLongDidNotReadChats chats,
        ITelegramBotClient botClient,
        ITimedTaskExecutionRepository repo,
        ILogger<DailyTooLongDidNotRead> logger) : base(repo, botClient, logger)
    {
        _command = command;
        _chats = chats;
    }

    protected override IEnumerable<long> ChatIds => _chats.Get();
    protected override string Name => "Daily TL;DR";
    protected override DateTime Date => DateTime.UtcNow;
    protected override string Type => "tldr";

    protected override async Task ExecuteForChat(long chatId)
    {
        await _command.Execute(chatId);
    }
}
