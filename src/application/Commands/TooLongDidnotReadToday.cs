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

    public async Task<Message> Execute(long chatId, CancellationToken cancellation = default)
    {
        await _botClient.SendChatActionAsync(
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
        return await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: telegramMessage,
            cancellationToken: cancellation);
    }

    private async IAsyncEnumerable<ChatCompletionCreateRequest> CreateGptRequest(
        long chatId,
        [EnumeratorCancellation] CancellationToken cancellation = default)
    {
        const float temperature = .2f;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var mb = ChatMessageBuilder.Create()
            .WithTokenCounter(_tokenCounter)
            .WithSystemRoleMessage(_whitelist.GetSystemRoleMessage(chatId));
        await foreach (var h in _history.Get(chatId, today, cancellation))
        {
            if (!mb.CanAddMessage)
            {
                mb.AddUserMessage(AskForTLDR);
                _logger.LogInformation("My tokens counter: {tokens}", mb.TokensCount);
                yield return new ChatCompletionCreateRequest
                {
                    Messages = mb.Build(),
                    Temperature = temperature,
                    Model = Models.Gpt_3_5_Turbo,
                };
            }

            mb.AddMessage(h, 1); // because of descending order of items in history
        }

        if (!mb.ContainsUserMessage)
        {
            yield break;
        }

        mb.AddUserMessage(AskForTLDR);
        var gptMessage = mb.Build();
        yield return new ChatCompletionCreateRequest
        {
            Messages = gptMessage,
            Temperature = temperature,
            Model = Models.Gpt_3_5_Turbo,
        };
    }

    private async Task<string> SendGptRequest(ChatCompletionCreateRequest request, CancellationToken cancellation = default)
    {
        var completionResult = await _openAiService.ChatCompletion.Create(
            request,
            Models.Model.Gpt_3_5_Turbo,
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
