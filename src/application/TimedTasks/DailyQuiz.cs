﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Bot;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using Ungerfall.ChatGpt.TelegramBot.Configuration;
using Ungerfall.ChatGpt.TelegramBot.Database;

namespace Ungerfall.ChatGpt.TelegramBot.TimedTasks;
public sealed class DailyQuiz : TimedTask
{
    public const string CRON_EXPRESSION = "0 0 6 * * *";

    private readonly QuizChats _quizChats;

    public DailyQuiz(
        QuizChats quizChats,
        ITelegramBotClient botClient,
        ITimedTaskExecutionRepository repo,
        ILogger<DailyQuiz> logger) : base(repo, botClient, logger)
    {
        _quizChats = quizChats;
    }

    protected override IEnumerable<long> ChatIds => _quizChats.Get();
    protected override string Name => "Daily computer science quiz";
    protected override DateTime Date => DateTime.UtcNow;
    protected override string Type => "quiz";

    protected override async Task ExecuteForChat(long chatId)
    {
        TimedTaskQuiz quiz = await _repo.GetQuiz(chatId, TimedTaskQuiz.Type_ComputerScience, cancellation: default)
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
