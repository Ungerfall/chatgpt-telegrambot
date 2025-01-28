using Microsoft.Extensions.Logging;
using OpenAI.Interfaces;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using Ungerfall.ChatGpt.TelegramBot.Configuration;

namespace Ungerfall.ChatGpt.TelegramBot.TimedTasks;
public class DailySummary : TimedTask
{
    public const string CRON_EXPRESSION = "0 55 23 * * *";

    // TODO: move to config/db
    private const string Prompt = """
            Напиши краткое содержание переписки.
            Если есть неотвеченные вопросы, перечисли их и ответь.
            В конце с новой строки добавь самые релевантные #теги переписки до 5 штук.
            """;
    private const float Temperature = .2f;

    private readonly DailySummaryChats _chats;
    private readonly IWhitelist _whitelist;
    private readonly ITelegramMessageRepository _messages;
    private readonly IOpenAIService _openAiService;
    private readonly string _model = Models.Gpt_4o;

    public DailySummary(
        DailySummaryChats chats,
        ITelegramBotClient botClient,
        ITimedTaskExecutionRepository repo,
        ILogger<DailySummary> logger,
        IWhitelist whitelist,
        ITelegramMessageRepository messages,
        IOpenAIService openAiService) : base(repo, botClient, logger)
    {
        _chats = chats;
        _whitelist = whitelist;
        _messages = messages;
        _openAiService = openAiService;
    }

    protected override IEnumerable<long> ChatIds => _chats.Get();
    protected override string Name => "Daily summary";
    protected override DateTime Date => DateTime.UtcNow;
    protected override string Type => "daily-summary";

    protected override async Task ExecuteForChat(long chatId)
    {
        var gptTasks = new List<Task<string>>();
        await foreach (var r in CreateGptRequestBatches(chatId, cancellation: default))
        {
            gptTasks.Add(SendGptRequest(r, cancellation: default));
        }

        var summaries = await Task.WhenAll(gptTasks);
        var telegramMessage = summaries.Length == 0
            ? "Сегодня ничего не произошло"
            : string.Join(Environment.NewLine, summaries);
        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: telegramMessage,
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
            cancellationToken: default);
    }

    private async IAsyncEnumerable<ChatCompletionCreateRequest> CreateGptRequestBatches(
        long chatId,
        [EnumeratorCancellation] CancellationToken cancellation = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var builder = ChatMessageBuilder.Create()
            .WithTokenCounter()
            .WithSystemRoleMessage(_whitelist.GetSystemRoleMessage(chatId));
        await foreach (var h in _messages.Get(chatId, today, cancellation))
        {
            // hit tokens limit
            if (!builder.CanAddMessage)
            {
                builder.AddUserMessage(Prompt);
                yield return new ChatCompletionCreateRequest
                {
                    Messages = builder.Build(),
                    Temperature = Temperature,
                    Model = _model,
                };
            }

            builder.AddMessage(h, 1); // descending order of items in history
        }

        if (!builder.ContainsUserMessage)
        {
            yield break;
        }

        builder.AddUserMessage(Prompt);
        yield return new ChatCompletionCreateRequest
        {
            Messages = builder.Build(),
            Temperature = Temperature,
            Model = _model,
        };
    }

    private async Task<string> SendGptRequest(ChatCompletionCreateRequest request, CancellationToken cancellation = default)
    {
        var completionResult = await _openAiService.ChatCompletion
            .Create(request, Models.Model.Gpt_4o, cancellationToken: cancellation);
        return completionResult.Successful
            ? completionResult?.Choices[0]?.Message?.Content ?? "Successful, but no content."
            : throw new ApplicationException(completionResult?.Error?.Message ?? "Error sending open ai api request");
    }
}
