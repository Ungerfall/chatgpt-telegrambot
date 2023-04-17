using Microsoft.Extensions.Logging;
using OpenAI.GPT3.Interfaces;
using OpenAI.GPT3.ObjectModels.RequestModels;
using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Ungerfall.ChatGpt.TelegramBot.Database;

namespace Ungerfall.ChatGpt.TelegramBot.Commands;
public class TooLongDidnotReadToday
{
    private const string AskForTLDR = "Напиши TL;DR всей истории переписки. Выведи статистику по пользователям. Избегай обобщений.";
    private readonly BriefTelegramMessageRepository _history;
    private readonly TokenCounter _tokenCounter;
    private readonly ITelegramBotClient _botClient;
    private readonly IOpenAIService _openAiService;
    private readonly ILogger<TooLongDidnotReadToday> _logger;

    public TooLongDidnotReadToday(
        BriefTelegramMessageRepository history,
        TokenCounter tokenCounter,
        IOpenAIService openAiService,
        ILogger<TooLongDidnotReadToday> logger,
        ITelegramBotClient botClient)
    {
        _history = history;
        _tokenCounter = tokenCounter;
        _openAiService = openAiService;
        _logger = logger;
        _botClient = botClient;
    }

    public async Task<Message> Execute(Message message, CancellationToken cancellation)
    {
        await _botClient.SendChatActionAsync(
            chatId: message.Chat.Id,
            ChatAction.Typing,
            cancellationToken: cancellation);
        var gptRequest = await CreateGptRequest(message, cancellation);
        var telegramMessage = await SendGptRequest(gptRequest, cancellation);
        telegramMessage = $"{telegramMessage}{Environment.NewLine}TL;DR не записывается в историю";
        return await _botClient.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: telegramMessage,
            replyToMessageId: message.MessageId,
            cancellationToken: cancellation);
    }

    private async Task<ChatCompletionCreateRequest> CreateGptRequest(Message message, CancellationToken cancellation)
    {
        var user = message.From?.Username ?? "unknown";
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var mb = ChatMessageBuilder.Create()
            .WithTokenCounter(_tokenCounter)
            .ForBriefAndConciseSystem();
        await foreach (var h in _history.Get(today, cancellation))
        {
            if (!mb.CanAddMessage)
            {
                break;
            }

            mb.AddMessage(h, 1); // because of descending order of items in history
        }

        mb.AddUserMessage(AskForTLDR);
        var gptMessage = mb.Build();
        _logger.LogInformation("My tokens counter: {tokens}", mb.TokensCount);
        return new ChatCompletionCreateRequest
        {
            Messages = gptMessage,
            Temperature = 0f,
            User = user,
        };
    }

    private async Task<string> SendGptRequest(ChatCompletionCreateRequest request, CancellationToken cancellation)
    {
        var completionResult = await _openAiService.ChatCompletion.CreateCompletion(
            request,
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
