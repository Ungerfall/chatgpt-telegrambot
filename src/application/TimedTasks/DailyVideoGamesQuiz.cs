using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using Ungerfall.ChatGpt.TelegramBot.Configuration;
using Ungerfall.ChatGpt.TelegramBot.Database;

namespace Ungerfall.ChatGpt.TelegramBot.TimedTasks;
public sealed class DailyVideoGamesQuiz : TimedTask
{
    public const string CRON_EXPRESSION = "0 0 6 * * *";

    private readonly QuizChats _quizChats;

    public DailyVideoGamesQuiz(
        QuizChats quizChats,
        ITelegramBotClient botClient,
        ITimedTaskExecutionRepository repo,
        ILogger<DailyVideoGamesQuiz> logger) : base(repo, botClient, logger)
    {
        _quizChats = quizChats;
    }

    protected override IEnumerable<long> ChatIds => _quizChats.Get();
    protected override string Name => "Daily video games quiz";
    protected override DateTime Date => DateTime.UtcNow;
    protected override string Type => TimedTaskQuiz.Type_VideoGames;

    protected override async Task ExecuteForChat(long chatId)
    {
        TimedTaskQuiz quiz = await _repo.GetQuiz(chatId, TimedTaskQuiz.Type_VideoGames, cancellation: default)
            ?? throw new ApplicationException("quizzes are not found. Ingest new quizzes.");

        await _botClient.SendPoll(
                    chatId: chatId,
                    question: quiz.Question,
                    options: quiz.Options.Select(x => new InputPollOption { Text = x }),
                    type: Telegram.Bot.Types.Enums.PollType.Quiz,
                    correctOptionId: quiz.CorrectOptionId,
                    explanation: quiz.Explanation,
                    explanationParseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown
            );
        await _repo.CompleteQuiz(quiz, cancellation: default);
    }
}
