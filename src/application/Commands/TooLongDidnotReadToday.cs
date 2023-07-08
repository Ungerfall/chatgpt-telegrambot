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
public class TooLongDidnotReadToday
{
    private const string AskForTLDR = "Напиши TL;DR переписки маркированным списком.";
    private readonly ITelegramMessageRepository _history;
    private readonly ITokenCounter _tokenCounter;
    private readonly ITelegramBotClient _botClient;
    private readonly IOpenAIService _openAiService;
    private readonly ILogger<TooLongDidnotReadToday> _logger;
    private readonly IWhitelist _whitelist;

    public TooLongDidnotReadToday(
        ITelegramMessageRepository history,
        ITokenCounter tokenCounter,
        IOpenAIService openAiService,
        ILogger<TooLongDidnotReadToday> logger,
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

    public async Task<Message> Execute(Message message, CancellationToken cancellation)
    {
        await _botClient.SendChatActionAsync(
            chatId: message.Chat.Id,
            ChatAction.Typing,
            cancellationToken: cancellation);
        var gptTasks = new List<Task<string>>();
        await foreach (var r in CreateGptRequest(message, cancellation))
        {
            gptTasks.Add(SendGptRequest(r, cancellation));
        }

        var summaries = await Task.WhenAll(gptTasks);
        var telegramMessage = summaries.Length == 0
            ? "Сегодня ничего не произошло"
            : $"{string.Join(NewLine, summaries)}{NewLine}TL;DR не записывается в историю";
        return await _botClient.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: telegramMessage,
            replyToMessageId: message.MessageId,
            cancellationToken: cancellation);
    }

    private async IAsyncEnumerable<ChatCompletionCreateRequest> CreateGptRequest(
        Message message,
        [EnumeratorCancellation] CancellationToken cancellation)
    {
        const float temperature = .2f;
        var user = message.From?.Username ?? "unknown";
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var mb = ChatMessageBuilder.Create()
            .WithTokenCounter(_tokenCounter)
            .WithSystemRoleMessage(_whitelist.GetSystemRoleMessage(message.Chat.Id));
        await foreach (var h in _history.Get(message.Chat.Id, today, cancellation))
        {
            if (!mb.CanAddMessage)
            {
                mb.AddUserMessage(AskForTLDR);
                _logger.LogInformation("My tokens counter: {tokens}", mb.TokensCount);
                yield return new ChatCompletionCreateRequest
                {
                    Messages = mb.Build(),
                    Temperature = temperature,
                    User = user,
                    Model = Models.Model.Gpt_4.EnumToString(),
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
            User = user,
            Model = Models.Model.Gpt_4.EnumToString(),
        };
    }

    private async Task<string> SendGptRequest(ChatCompletionCreateRequest request, CancellationToken cancellation)
    {
        var completionResult = await _openAiService.ChatCompletion.Create(
            request,
            Models.Model.Gpt_4,
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
            return completionResult.Choices[0].Message.Content;
        }
        else
        {
            _logger.LogError("ChatGPT error: {@error}", new { completionResult.Error?.Type, completionResult.Error?.Message });
            return "ChatGPT request wasn't successful";
        }
    }
}
