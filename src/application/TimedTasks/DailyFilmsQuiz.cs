﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Bot;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using Ungerfall.ChatGpt.TelegramBot.Configuration;
using Ungerfall.ChatGpt.TelegramBot.Database;

namespace Ungerfall.ChatGpt.TelegramBot.TimedTasks;
public sealed class DailyFilmsQuiz : TimedTask
{
    public const string CRON_EXPRESSION = "0 0 6 * * *";

    private readonly QuizChats _quizChats;

    public DailyFilmsQuiz(
        QuizChats quizChats,
        ITelegramBotClient botClient,
        ITimedTaskExecutionRepository repo,
        ILogger<DailyFilmsQuiz> logger) : base(repo, botClient, logger)
    {
        _quizChats = quizChats;
    }

    protected override IEnumerable<long> ChatIds => _quizChats.Get();
    protected override string Name => "Daily films quiz";
    protected override DateTime Date => DateTime.UtcNow;
    protected override string Type => TimedTaskQuiz.Type_ComputerScience;

    protected override async Task ExecuteForChat(long chatId)
    {
        TimedTaskQuiz quiz = await _repo.GetQuiz(chatId, TimedTaskQuiz.Type_Films, cancellation: default)
            ?? throw new ApplicationException("quizzes are not found. Ingest new quizzes.");

        await _botClient.SendPollAsync(
                    chatId: chatId,
                    question: quiz.Question,
                    quiz.Options,
                    type: Telegram.Bot.Types.Enums.PollType.Quiz,
                    correctOptionId: quiz.CorrectOptionId,
                    explanation: quiz.Explanation,
                    explanationParseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown
            );
        await _repo.CompleteQuiz(quiz, cancellation: default);
    }
}
