using Microsoft.Extensions.Logging;
using OpenAI.Interfaces;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Telegram.Bot;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using Ungerfall.ChatGpt.TelegramBot.Configuration;
using Ungerfall.ChatGpt.TelegramBot.SourceGenerators;

namespace Ungerfall.ChatGpt.TelegramBot.TimedTasks;
public sealed class DailyQuiz : TimedTask
{
    public const string CRON_EXPRESSION = "0 0 6 * * *";

    private readonly QuizChats _quizChats;
    private readonly IOpenAIService _openAiService;

    public DailyQuiz(
        QuizChats quizChats,
        ITelegramBotClient botClient,
        ITimedTaskExecutionRepository repo,
        ILogger<DailyQuiz> logger,
        IOpenAIService openAiService) : base(repo, botClient, logger)
    {
        _quizChats = quizChats;
        _openAiService = openAiService;
    }

    protected override IEnumerable<long> ChatIds => _quizChats.Get();
    protected override string Name => "Daily computer science quiz";
    protected override DateTime Date => DateTime.UtcNow;
    protected override string Type => "quiz";

    protected override async Task ExecuteQuiz(long chatId)
    {
        var gptMessageToCreateQuiz = ChatMessageBuilder
            .Create()
            .WithTokenCounter()
            .WithSystemRoleMessage("""
            Ты - эксперт по составлению викторин по информатике и программной инженерии.
            Язык: русский, но не переводи термины.
            Сложность: средняя и выше.
            """)
            .AddUserMessage("""
            Составь JSON с викториной соответсвующей следующей JSON schema
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.com/product.schema.json",
              "title": "Викторина",
              "description": "Викторина по инфроматике и программной инженерии",
              "type": "object",
              "properties": {
                "question": {
                  "description": "Вопрос викторины",
                  "type": "string",
                  "minLength": 1,
                  "maxLength": 300
                },
                "options": {
                  "description": "От 4 до 8 вариантов ответов",
                  "type": "array",
                  "minItems": 4,
                  "maxItems": 8,
                  "uniqueItems": true,
                  "items": {
                    "type": "string"
                  }
                },
                "correctOptionId": {
                  "description": "Идентификатор правильного варианта ответа, начинающийся с 0",
                  "type": "integer",
                  "minimum": 0
                },
                "explanation": {
                  "description": "Короткое объяснение правильного варианта",
                  "type": "string",
                  "maxLength": 100
                }
              }
            }
            """)
            .Build();
        var completionResult = await _openAiService.ChatCompletion.Create(
            new ChatCompletionCreateRequest
            {
                Messages = gptMessageToCreateQuiz,
                Temperature = 2.0f,
                User = chatId.ToString(),
                Model = Models.Model.Gpt_4.EnumToString()
            },
            Models.Model.Gpt_4,
            cancellationToken: default);
        string? content = completionResult?.Choices[0]?.Message?.Content;
        if (!(completionResult?.Successful ?? false) || content is null)
        {
            throw new ApplicationException("ChatGPT request wasn't successful");
        }

        Quiz? quiz = JsonSerializer.Deserialize(content, QuizContext.Default.Quiz)
            ?? throw new ApplicationException("Cannot deserialize quiz from chatgpt");

        await _botClient.SendPollAsync(
                    chatId: chatId,
                    question: quiz.Question,
                    quiz.Options,
                    type: Telegram.Bot.Types.Enums.PollType.Quiz,
                    correctOptionId: quiz.CorrectOptionId,
                    explanation: quiz.Explanation,
                    explanationParseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown
            );
    }
}
