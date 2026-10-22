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
using Ungerfall.ChatGpt.TelegramBot.Configuration;

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
    private readonly TestUsers _testUsers;
    private readonly IConversationRepository _conversations;
    private readonly ConversationPolicy _conversationPolicy;

    public UpdateHandler(
        ITelegramBotClient botClient,
        ILogger<UpdateHandler> logger,
        IOpenAIService openAiService,
        ITelegramMessageRepository telegramMessagesRepository,
        ITokenCounter tokenCounter,
        TooLongDidNotReadToday tooLongDidnotReadCommand,
        IWhitelist whitelist,
        GenerateImage imageCommand,
        TestUsers testUsers,
        IConversationRepository conversations,
        ConversationPolicy conversationPolicy)
    {
        _botClient = botClient;
        _logger = logger;
        _openAiService = openAiService;
        _telegramMessagesRepository = telegramMessagesRepository;
        _tokenCounter = tokenCounter;
        _tooLongDidnotReadCommand = tooLongDidnotReadCommand;
        _whitelist = whitelist;
        _imageCommand = imageCommand;
        _testUsers = testUsers;
        _conversations = conversations;
        _conversationPolicy = conversationPolicy;
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
        if (string.IsNullOrWhiteSpace(message.Text) || message.From?.IsBot == true || IsBot(message.From))
            return;
        var messageText = message.Text;

        bool allowed = _whitelist.IsGroupAllowedToUseBot(message.Chat.Id)
            || _testUsers.Get().Contains(message.Chat.Id);
        if (!allowed)
        {
            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: $"""
                Only for my chat.
                You:
                ```{JsonSerializer.Serialize(message.Chat, typeof(Chat), SourceGenerators.TelegramChatContext.Default)}```
                """,
                parseMode: ParseMode.Markdown,
                replyParameters: message.Id,
                cancellationToken: cancellation);
            return;
        }

        var receipt = await _conversations.Begin(message.Chat.Id, message.Id, cancellation);
        if (receipt == null) return;
        try
        {
            int commandEndIndex = messageText.GetCommandEndIndex();
            string command = messageText[0] == '/' ? messageText[..commandEndIndex].ToLowerInvariant() : string.Empty;
            bool otherBotCommand = false;
            if (command.Length > 0 && commandEndIndex < messageText.Length && messageText[commandEndIndex] == '@')
            {
                var targetEnd = messageText.IndexOf(' ', commandEndIndex);
                if (targetEnd < 0) targetEnd = messageText.Length;
                otherBotCommand = !messageText[commandEndIndex..targetEnd].Equals(BotUsername, StringComparison.OrdinalIgnoreCase);
                commandEndIndex = targetEnd;
                if (otherBotCommand) command = string.Empty;
            }

            async Task BeforeSend()
            {
                receipt.Stage = Database.ConversationReceipt.Sending;
                try
                {
                    await _conversations.SaveReceipt(receipt, cancellation);
                }
                catch
                {
                    receipt.Stage = Database.ConversationReceipt.Processing;
                    throw;
                }
            }

            string msgWithoutCommand = messageText[commandEndIndex..];
            var action = command switch
            {
                "/image" => _imageCommand.Execute(message, msgWithoutCommand, cancellation, BeforeSend),
                "/tldrtoday" => _tooLongDidnotReadCommand.Execute(
                    message.Chat.Id, cancellation,
                    useRich: message.Chat.Type is ChatType.Group or ChatType.Supergroup,
                    messageThreadId: message.MessageThreadId,
                    beforeSend: BeforeSend),
                _ => OnMessageReceived(message, messageText, command.Length > 0, otherBotCommand, BeforeSend, cancellation),
            };
            await action;
            if (command is "/image" or "/tldrtoday")
            {
                await _conversationPolicy.Observe(message, null, null, cancellation);
            }
            receipt.Stage = Database.ConversationReceipt.Completed;
            await _conversations.SaveReceipt(receipt, cancellation);
        }
        catch
        {
            if (receipt.Stage == Database.ConversationReceipt.Processing)
            {
                try
                {
                    await _conversations.Release(receipt, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to release message ({ChatId}, {MessageId}) for retry", message.Chat.Id, message.Id);
                }
            }
            throw;
        }
    }

    private async Task<Message> OnMessageReceived(
        Message msg,
        string messageText,
        bool isCommand,
        bool otherBotCommand,
        Func<Task> beforeSend,
        CancellationToken cancellation)
    {
        bool activateBot = !otherBotCommand && (msg.Chat.Type == ChatType.Private || isCommand
            || IsBot(msg.ReplyToMessage?.From)
            || ((msg.Entities?.Any(x => x.Type == MessageEntityType.Mention) ?? false)
                && (msg.EntityValues?.Any(x => x.Equals(BotUsername, StringComparison.OrdinalIgnoreCase)) ?? false))
            || (msg.Entities?.Any(x => x.Type == MessageEntityType.TextMention && IsBot(x.User)) ?? false));
        var user = msg.From?.FirstName ?? msg.From?.Username ?? "unknown";
        var chatId = msg.Chat.Id;
        if (!activateBot)
        {
            _logger.LogInformation("Bot action is not expected");
            await SaveToHistory(chatId, UserId(msg), msg.Text!, msg.Id, msg.Date, user, cancellation);
            await _conversationPolicy.Observe(msg, null, null, cancellation);
            return msg;
        }

        await _botClient.SendChatAction(
            chatId: chatId,
            ChatAction.Typing,
            cancellationToken: cancellation);
        var (chatGptResponse, tokens) = await SendChatGptMessage(messageText, user, chatId, cancellation);
        await beforeSend();
        var sent = await _botClient.SendResponse(
            chatId: chatId,
            text: $"{chatGptResponse}{Environment.NewLine}tokens: {tokens}",
            useRich: msg.Chat.Type is ChatType.Group or ChatType.Supergroup,
            replyParameters: msg.Id,
            messageThreadId: msg.MessageThreadId,
            cancellationToken: cancellation);
        await SaveToHistory(chatId, UserId(msg), msg.Text!, msg.Id, msg.Date, user, cancellation);
        await SaveToHistory(chatId, UserId(sent), chatGptResponse, sent.Id, sent.Date, BotUsername[1..], cancellation);
        await _conversationPolicy.Observe(msg, sent, chatGptResponse, cancellation);
        _logger.LogInformation("The message was sent with id: {SentMessageId}", sent.Id);
        return sent;

        static long UserId(Message msg) => msg.From?.Id ?? default;
    }

    private bool IsBot(User? user) => user != null
        && ((_botClient.BotId != 0 && user.Id == _botClient.BotId)
            || string.Equals(user.Username, BotUsername[1..], StringComparison.OrdinalIgnoreCase));

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
                Model = Models.Gpt_4o_mini
            },
            Models.Model.Gpt_4o_mini,
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
            return (completionResult?.Choices[0]?.Message?.Content ?? "Successful, but no content.", completionResult?.Usage?.TotalTokens);
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
