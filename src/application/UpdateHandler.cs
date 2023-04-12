using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using OpenAI.GPT3.Interfaces;
using OpenAI.GPT3.ObjectModels.RequestModels;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Ungerfall.ChatGpt.TelegramBot.Commands;
using Ungerfall.ChatGpt.TelegramBot.Database;
using Ungerfall.ChatGpt.TelegramBot.Queue;

namespace Ungerfall.ChatGpt.TelegramBot;

public class UpdateHandler
{
    private const long OurChatGroupId = -1001034436662;
    private const string BotUsername = "@chatgpt_ungerfall_bot";

    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<UpdateHandler> _logger;
    private readonly IOpenAIService _openAiService;
    private readonly ServiceBusClient _serviceBus;
    private readonly BriefTelegramMessageRepository _history;
    private readonly TokenCounter _tokenCounter;
    private readonly TooLongDidnotReadToday _tooLongDidnotReadCommand;

    public UpdateHandler(
        ITelegramBotClient botClient,
        ILogger<UpdateHandler> logger,
        IOpenAIService openAiService,
        ServiceBusClient serviceBus,
        BriefTelegramMessageRepository history,
        TokenCounter tokenCounter,
        TooLongDidnotReadToday tooLongDidnotReadCommand)
    {
        _botClient = botClient;
        _logger = logger;
        _openAiService = openAiService;
        _serviceBus = serviceBus;
        _history = history;
        _tokenCounter = tokenCounter;
        _tooLongDidnotReadCommand = tooLongDidnotReadCommand;
    }

    public async Task Handle(Update update, CancellationToken cancellation)
    {
        _logger.LogInformation("Invoke telegram update function");

        var handler = update switch
        {
            /*
             { EditedMessage: { } }      => UpdateType.EditedMessage,
             { InlineQuery: { } }        => UpdateType.InlineQuery,
             { ChosenInlineResult: { } } => UpdateType.ChosenInlineResult,
             { CallbackQuery: { } }      => UpdateType.CallbackQuery,
             { ChannelPost: { } }        => UpdateType.ChannelPost,
             { EditedChannelPost: { } }  => UpdateType.EditedChannelPost,
             { ShippingQuery: { } }      => UpdateType.ShippingQuery,
             { PreCheckoutQuery: { } }   => UpdateType.PreCheckoutQuery,
             { Poll: { } }               => UpdateType.Poll,
             { PollAnswer: { } }         => UpdateType.PollAnswer,
             { MyChatMember: { } }       => UpdateType.MyChatMember,
             { ChatMember: { } }         => UpdateType.ChatMember,
             { ChatJoinRequest: { } }    => UpdateType.ChatJoinRequest,
            */
            { Message: { } message } => BotOnMessageReceived(message, cancellation),
            _ => UnknownUpdateHandlerAsync(update, cancellation)
        };

        await handler;
    }

    private async Task BotOnMessageReceived(Message message, CancellationToken cancellation)
    {
        _logger.LogInformation("Receive message type: {MessageType}. Group: {GroupId}", message.Type, message.Chat.Id);
        if (message.Text is not { } messageText)
            return;

        if (message.Chat.Id != OurChatGroupId)
        {
            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Only for my chat.",
                replyToMessageId: message.MessageId,
                cancellationToken: cancellation);
            return;
        }

        bool containMention = message.Entities?.Any(x => x.Type == MessageEntityType.Mention) ?? false;
        bool isBotMentioned = containMention && (message.EntityValues?.Any(x => x.Equals(BotUsername)) ?? false);
        if (!isBotMentioned)
        {
            _logger.LogInformation("The message does not contain mention of bot.");
            _ = SendMessageToConversationHistory(message, cancellation);
            return;
        }

        var action = messageText.Split(' ')[0] switch
        {
            "/tldrtoday" => _tooLongDidnotReadCommand.Execute(cancellation),
            _ => BotMentionedMessage(message, messageText, cancellation),
        };

        var sentMsg = await action;
        _ = SendMessageToConversationHistory(message, cancellation);
        _ = SendMessageToConversationHistory(sentMsg, cancellation);

        _logger.LogInformation("The message was sent with id: {SentMessageId}", sentMsg.MessageId);
    }

    private async Task<Message> BotMentionedMessage(Message message, string messageText, CancellationToken cancellation)
    {
        await _botClient.SendChatActionAsync(
            chatId: message.Chat.Id,
            ChatAction.Typing,
            cancellationToken: cancellation);
        var user = message.From?.Username ?? "unknown";
        var (chatGptResponse, tokens) = await SendChatGptMessage(messageText, user, cancellation);
        chatGptResponse = $"{chatGptResponse}{Environment.NewLine}tokens: {tokens}";
        return await _botClient.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: chatGptResponse,
            replyToMessageId: message.MessageId,
            cancellationToken: cancellation);
    }

    private async Task SendMessageToConversationHistory(Message msg, CancellationToken cancellation)
    {
        var q = _serviceBus.CreateSender(QueueTelegramMessage.QUEUE_NAME);
        var qMsg = new ServiceBusMessage(JsonSerializer.Serialize(new QueueTelegramMessage
        {
            User = msg.From?.FirstName ?? msg.From?.Username ?? "unknown",
            UserId = msg.From?.Id ?? default,
            Message = msg.Text!, // null check upwards
            MessageId = msg.MessageId,
            Date = msg.Date,
        }));
        await q.SendMessageAsync(qMsg, cancellation);
    }

    private async Task<(string, int)> SendChatGptMessage(string message, string user, CancellationToken cancellation)
    {
        _logger.LogInformation("Sending {Message} from {User}", message, user);

        var history = new ConversationHistory(_history, $"{user}: {message}", _tokenCounter);
        var (gptMessage, tokensCount) = await history.GetForChatGpt(cancellation);
        var completionResult = await _openAiService.ChatCompletion.CreateCompletion(
            new ChatCompletionCreateRequest
            {
                Messages = gptMessage,
                Temperature = 0f,
                User = user,
            },
            cancellationToken: cancellation);
        _logger.LogInformation("Tokens: {@tokens}",
            new
            {
                MyTokensCounter = tokensCount,
                completionResult.Usage.CompletionTokens,
                completionResult.Usage.PromptTokens,
                completionResult.Usage.TotalTokens
            });
        if (completionResult.Successful)
        {
            return (completionResult.Choices[0].Message.Content, completionResult.Usage.TotalTokens);
        }
        else
        {
            _logger.LogError("ChatGPT error: {@error}", new { completionResult.Error?.Type, completionResult.Error?.Message });
            return ("ChatGPT request wasn't successful", completionResult.Usage.TotalTokens);
        }
    }

    private Task UnknownUpdateHandlerAsync(Update update, CancellationToken _)
    {
        _logger.LogInformation("Unknown update type: {UpdateType}", update.Type);
        return Task.CompletedTask;
    }

    public async Task HandlePollingErrorAsync(Exception exception, CancellationToken cancellationToken)
    {
        var ErrorMessage = exception switch
        {
            ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        _logger.LogInformation("HandleError: {ErrorMessage}", ErrorMessage);

        // Cooldown in case of network connection error
        if (exception is RequestException)
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
    }
}
