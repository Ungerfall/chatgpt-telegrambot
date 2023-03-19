using OpenAI.GPT3.Interfaces;
using OpenAI.GPT3.ObjectModels.RequestModels;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace ChatGPT.TelegramBot.Services;

public class UpdateHandler : IUpdateHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<UpdateHandler> _logger;
    private readonly IOpenAIService _openAiService;

    public UpdateHandler(ITelegramBotClient botClient, ILogger<UpdateHandler> logger, IOpenAIService openAiService)
    {
        _botClient = botClient;
        _logger = logger;
        _openAiService = openAiService;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient _, Update update, CancellationToken cancellationToken)
    {
        var handler = update switch
        {
            // UpdateType.Unknown:
            // UpdateType.ChannelPost:
            // UpdateType.EditedChannelPost:
            // UpdateType.ShippingQuery:
            // UpdateType.PreCheckoutQuery:
            // UpdateType.Poll:
            { Message: { } message } => BotOnMessageReceived(message, cancellationToken),
            { EditedMessage: { } message } => BotOnMessageReceived(message, cancellationToken),
            _ => UnknownUpdateHandlerAsync(update, cancellationToken)
        };

        await handler;
    }

    private async Task BotOnMessageReceived(Message message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Receive message type: {MessageType}. Group: {GroupId}", message.Type, message.Chat.Id);
        if (message.Text is not { } messageText)
            return;

        if (message.Chat.Id != -1034436662)
        {
            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Only for my chat.",
                replyToMessageId: message.MessageId,
                cancellationToken: cancellationToken);
            return;
        }

        var chatGptResponse = await SendChatGptMessage(message.Text, message.From?.Username ?? "unknown", cancellationToken);
        Message sentMessage = await _botClient.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: chatGptResponse,
            cancellationToken: cancellationToken);

        _logger.LogInformation("The message was sent with id: {SentMessageId}", sentMessage.MessageId);
    }

    private async Task<string> SendChatGptMessage(string message, string user, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Sending {Message} from {User}", message, user);

        var completionResult = await _openAiService.ChatCompletion.CreateCompletion(
            new ChatCompletionCreateRequest
            {
                Messages = new[]
                {
                    ChatMessage.FromSystem("You are a helpful assistant."),
                    ChatMessage.FromUser(message),
                },
                Temperature = 0f,
                User = user,
            },
            cancellationToken: cancellationToken);
        if (completionResult.Successful)
        {
            return completionResult.Choices[0].Message.Content;
        }
        else
        {
            return "ChatGPT request wasn't successful";
        }
    }

    private Task UnknownUpdateHandlerAsync(Update update, CancellationToken _)
    {
        _logger.LogInformation("Unknown update type: {UpdateType}", update.Type);
        return Task.CompletedTask;
    }

    public async Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
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