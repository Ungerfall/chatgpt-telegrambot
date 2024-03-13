using Microsoft.Extensions.Logging;
using OpenAI.Interfaces;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using Ungerfall.ChatGpt.TelegramBot.Commands;

namespace Ungerfall.ChatGpt.TelegramBot;

public class UpdateHandler
{
    private const string BotUsername = "@chatgpt_ungerfall_bot";

    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<UpdateHandler> _logger;
    private readonly IOpenAIService _openAiService;
    private readonly ITelegramMessageRepository _telegramMessagesRepository;
    private readonly ITokenCounter _tokenCounter;
    private readonly TooLongDidNotReadToday _tooLongDidnotReadCommand;
    private readonly GenerateImage _imageCommand;
    private readonly IWhitelist _whitelist;

    public UpdateHandler(
        ITelegramBotClient botClient,
        ILogger<UpdateHandler> logger,
        IOpenAIService openAiService,
        ITelegramMessageRepository telegramMessagesRepository,
        ITokenCounter tokenCounter,
        TooLongDidNotReadToday tooLongDidnotReadCommand,
        IWhitelist whitelist,
        GenerateImage imageCommand)
    {
        _botClient = botClient;
        _logger = logger;
        _openAiService = openAiService;
        _telegramMessagesRepository = telegramMessagesRepository;
        _tokenCounter = tokenCounter;
        _tooLongDidnotReadCommand = tooLongDidnotReadCommand;
        _whitelist = whitelist;
        _imageCommand = imageCommand;
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
        _logger.LogInformation("Receive message {@message}", new { message.Text, message.Type, message.Chat.Id });
        if (message.Text is not { } messageText)
            return;

        if (!_whitelist.IsGroupAllowedToUseBot(message.Chat.Id))
        {
            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                parseMode: ParseMode.Markdown,
                text: $"""
                Only for my chat.
                You:
                ```{JsonSerializer.Serialize(message.Chat, new JsonSerializerOptions { WriteIndented = true, MaxDepth = 3 })}```
                """,
                replyToMessageId: message.MessageId,
                cancellationToken: cancellation);
            return;
        }

        int commandEndIndex = messageText.GetCommandEndIndex();
        string command = messageText[0] == '/'
            ? messageText[0..commandEndIndex]
            : string.Empty;
        string msgWithoutCommand = messageText[commandEndIndex..];
        var action = command switch
        {
            "/image" => _imageCommand.Execute(message, msgWithoutCommand, cancellation),
            "/tldrtoday" => _tooLongDidnotReadCommand.Execute(message, cancellation),
            _ => OnMessageReceived(message, messageText, cancellation),
        };
        await action;
    }

    private async Task<Message> OnMessageReceived(Message msg, string messageText, CancellationToken cancellation)
    {
        bool containMention = msg.Entities?.Any(x => x.Type == MessageEntityType.Mention) ?? false;
        bool isBotMentioned = containMention && (msg.EntityValues?.Any(x => x.Equals(BotUsername)) ?? false);
        var user = msg.From?.FirstName ?? msg.From?.Username ?? "unknown";
        var chatId = msg.Chat.Id;
        if (!isBotMentioned)
        {
            _logger.LogInformation("The message does not contain mention of bot.");
            _ = SaveToHistory(chatId, UserId(msg), msg.Text!, msg.MessageId, msg.Date, user, cancellation);
            return msg;
        }

        await _botClient.SendChatActionAsync(
            chatId: chatId,
            ChatAction.Typing,
            cancellationToken: cancellation);
        var (chatGptResponse, tokens) = await SendChatGptMessage(messageText, user, chatId, cancellation);
        var sent = await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: $"{chatGptResponse}{Environment.NewLine}tokens: {tokens}",
            replyToMessageId: msg.MessageId,
            cancellationToken: cancellation);
        await SaveToHistory(chatId, UserId(msg), msg.Text!, msg.MessageId, msg.Date, user, cancellation);
        await SaveToHistory(chatId, UserId(sent), sent.Text!, sent.MessageId, sent.Date, user, cancellation);
        _logger.LogInformation("The message was sent with id: {SentMessageId}", sent.MessageId);
        return sent;

        static long UserId(Message msg) => msg.From?.Id ?? default;
    }

    private async Task SaveToHistory(
        long chatId,
        long userId,
        string message,
        int messageId,
        DateTime date,
        string user,
        CancellationToken cancellation)
    {
        await _telegramMessagesRepository.Create(
            new Database.TelegramMessage
            {
                ChatId = chatId,
                User = user,
                UserId = userId,
                Message = message,
                MessageId = messageId,
                Date = date,
            },
            cancellation);
    }

    private async Task<(string, int?)> SendChatGptMessage(string message, string user, long chatId, CancellationToken cancellation)
    {
        _logger.LogInformation("Sending {Message} from {User}", message, user);

        var history = new ConversationHistory(_telegramMessagesRepository, $"{user}: {message}", _tokenCounter, _whitelist);
        var (gptMessage, tokensCount) = await history.GetForChatGpt(chatId, cancellation);
        var completionResult = await _openAiService.ChatCompletion.Create(
            new ChatCompletionCreateRequest
            {
                Messages = gptMessage,
                Temperature = 0f,
                User = user,
                Model = Models.Model.Gpt_3_5_Turbo.EnumToString()
            },
            Models.Model.Gpt_3_5_Turbo,
            cancellationToken: cancellation);
        _logger.LogInformation("Tokens: {@tokens}",
            new
            {
                MyTokensCounter = tokensCount,
                completionResult.Usage?.CompletionTokens,
                completionResult.Usage?.PromptTokens,
                completionResult.Usage?.TotalTokens
            });
        if (completionResult.Successful)
        {
            return (completionResult.Choices[0].Message.Content, completionResult.Usage?.TotalTokens);
        }
        else
        {
            _logger.LogError("ChatGPT error: {@error}", new { completionResult.Error?.Type, completionResult.Error?.Message });
            return ("ChatGPT request wasn't successful", completionResult.Usage?.TotalTokens);
        }
    }

    private Task UnknownUpdateHandlerAsync(Update update, CancellationToken _)
    {
        _logger.LogInformation("Unknown update type: {UpdateType}", update.Type);
        return Task.CompletedTask;
    }

    public async Task HandlePollingErrorAsync(Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        _logger.LogInformation("HandleError: {ErrorMessage}", errorMessage);

        // Cooldown in case of network connection error
        if (exception is RequestException)
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
    }
}
