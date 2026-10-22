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
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using static System.Environment;

namespace Ungerfall.ChatGpt.TelegramBot.Commands;
public class TooLongDidNotReadToday
{
    private const string AskForTLDR = "Напиши TL;DR переписки маркированным списком.";
    private readonly ITelegramMessageRepository _history;
    private readonly ITokenCounter _tokenCounter;
    private readonly ITelegramBotClient _botClient;
    private readonly IOpenAIService _openAiService;
    private readonly ILogger<TooLongDidNotReadToday> _logger;
    private readonly IWhitelist _whitelist;

    public TooLongDidNotReadToday(
        ITelegramMessageRepository history,
        ITokenCounter tokenCounter,
        IOpenAIService openAiService,
        ILogger<TooLongDidNotReadToday> logger,
        ITelegramBotClient botClient,
        IWhitelist whitelist)
    {
        _history = history;
        _tokenCounter = tokenCounter;
        _openAiService = openAiService;
        _logger = logger;
        _botClient = botClient;
        _whitelist = whitelist;
    }

    public async Task<Message> Execute(
        long chatId,
        CancellationToken cancellation = default,
        bool useRich = false,
        int? messageThreadId = default,
        Func<Task>? beforeSend = default)
    {
        await _botClient.SendChatAction(
            chatId: chatId,
            ChatAction.Typing,
            cancellationToken: cancellation);
        var gptTasks = new List<Task<string>>();
        await foreach (var r in CreateGptRequest(chatId, cancellation))
        {
            gptTasks.Add(SendGptRequest(r, cancellation));
        }

        var summaries = await Task.WhenAll(gptTasks);
        var telegramMessage = summaries.Length == 0
            ? "Сегодня ничего не произошло"
            : $"{string.Join(NewLine, summaries)}{NewLine}TL;DR не записывается в историю";
        if (beforeSend != null) await beforeSend();
        return await _botClient.SendResponse(
            chatId: chatId,
            text: telegramMessage,
            useRich: useRich,
            messageThreadId: messageThreadId,
            cancellationToken: cancellation);
    }

    private async IAsyncEnumerable<ChatCompletionCreateRequest> CreateGptRequest(
        long chatId,
        [EnumeratorCancellation] CancellationToken cancellation = default)
    {
        const float temperature = .2f;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var systemRole = _whitelist.GetSystemRoleMessage(chatId);
        await foreach (var batch in ChatMessageBuilder.CreateBatches(
            _history.Get(chatId, today, cancellation), systemRole, AskForTLDR, _tokenCounter, cancellation))
        {
            yield return new ChatCompletionCreateRequest { Messages = batch, Temperature = temperature, Model = Models.Gpt_4o_mini };
        }
    }

    private async Task<string> SendGptRequest(ChatCompletionCreateRequest request, CancellationToken cancellation = default)
    {
        var completionResult = await _openAiService.ChatCompletion.Create(
            request,
            Models.Model.Gpt_4o_mini,
            cancellationToken: cancellation);
        _logger.LogInformation("Tokens: {@tokens}",
            new
            {
                completionResult.Usage?.CompletionTokens,
                completionResult.Usage?.PromptTokens,
                completionResult.Usage?.TotalTokens
            });
        if (completionResult.Successful)
        {
            return completionResult?.Choices[0]?.Message?.Content ?? "Successful, but no content.";
        }
        else
        {
            _logger.LogError("ChatGPT error: {@error}", new { completionResult.Error?.Type, completionResult.Error?.Message });
            return "ChatGPT request wasn't successful";
        }
    }
}
