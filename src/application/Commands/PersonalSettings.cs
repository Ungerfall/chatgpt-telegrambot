using Microsoft.Extensions.Logging;
using OpenAI.ObjectModels;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Ungerfall.ChatGpt.TelegramBot.Commands;
public class PersonalSettings
{
    private const string OpenPrivateChat = "Откройте личный чат с ботом и отправьте /settings.";
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<PersonalSettings> _logger;

    public PersonalSettings(ITelegramBotClient botClient, ILogger<PersonalSettings> logger)
    {
        _botClient = botClient;
        _logger = logger;
    }

    public async Task Execute(Message message, CancellationToken cancellation, Func<Task>? beforeSend = default)
    {
        if (message.From is not { IsBot: false } user) return;
        bool group = message.Chat.Type is ChatType.Group or ChatType.Supergroup;
        if (!group && (message.Chat.Type != ChatType.Private || message.Chat.Id != user.Id)) return;
        if (message.EphemeralMessageId != null && (!group || DateTime.UtcNow - message.Date > TimeSpan.FromSeconds(15))) return;
        if (beforeSend != null) await beforeSend();

        try
        {
            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: group && message.EphemeralMessageId == null ? OpenPrivateChat : GetText("open")!,
                messageThreadId: message.MessageThreadId,
                replyParameters: message.EphemeralMessageId is { } ephemeralId
                    ? new ReplyParameters { EphemeralMessageId = ephemeralId } : null,
                replyMarkup: Keyboard(user.Id, group && message.EphemeralMessageId == null),
                ephemeralMessageParameters: message.EphemeralMessageId != null
                    ? new EphemeralMessageParameters { ReceiverUserId = user.Id } : null,
                cancellationToken: cancellation);
        }
        catch (ApiRequestException ex) when (message.EphemeralMessageId != null && ex.ErrorCode is 400 or 403 or 404)
        {
            _logger.LogWarning("Ephemeral settings unavailable: Telegram error {ErrorCode}", ex.ErrorCode);
            await _botClient.SendResponse(message.Chat.Id, OpenPrivateChat,
                messageThreadId: message.MessageThreadId, cancellationToken: cancellation);
        }
        catch (RequestException ex) when (ex is not ApiRequestException)
        {
            throw new TelegramDeliveryUnknownException(ex);
        }
    }

    public async Task Execute(CallbackQuery query, CancellationToken cancellation)
    {
        var message = query.Message;
        var parts = query.Data?.Split(':');
        if (message == null || query.From.IsBot || _botClient.BotId == 0 || message.From?.Id != _botClient.BotId
            || parts is not { Length: 3 } || parts[0] != "settings"
            || !long.TryParse(parts[1], out var owner) || owner != query.From.Id
            || !(message.ReplyMarkup?.InlineKeyboard.SelectMany(x => x).Any(x => x.CallbackData == query.Data) ?? false)
            || (message.Chat.Type == ChatType.Private ? message.Chat.Id != owner
                : message.Chat.Type is not (ChatType.Group or ChatType.Supergroup)
                    || (message.EphemeralMessageId != null ? message.ReceiverUser?.Id != owner
                        : message.ReceiverUser != null || message.Text != OpenPrivateChat || parts[2] != "open")))
        {
            await Answer(query, cancellation, "Отправьте /settings, чтобы открыть свою справку.");
            return;
        }

        var text = GetText(parts[2]);
        if (text == null) return;
        try
        {
            if (message.EphemeralMessageId is { } ephemeralId)
            {
                if (message.Text != text)
                    await _botClient.EditEphemeralMessageText(message.Chat.Id, owner, ephemeralId, text,
                        replyMarkup: Keyboard(owner), cancellationToken: cancellation);
            }
            else if (message.Chat.Type == ChatType.Private)
            {
                if (message.Text != text)
                    await _botClient.EditMessageText(message.Chat.Id, message.Id, text,
                        replyMarkup: Keyboard(owner), cancellationToken: cancellation);
            }
            else
            {
                await _botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: text,
                    messageThreadId: message.MessageThreadId,
                    replyMarkup: Keyboard(owner),
                    ephemeralMessageParameters: new EphemeralMessageParameters { ReceiverUserId = owner, CallbackQueryId = query.Id },
                    cancellationToken: cancellation);
            }
            await Answer(query, cancellation);
        }
        catch (ApiRequestException ex) when (ex.ErrorCode is 400 or 403 or 404)
        {
            _logger.LogWarning("Settings unavailable: Telegram error {ErrorCode}", ex.ErrorCode);
            await Answer(query, cancellation, OpenPrivateChat, showAlert: true);
            return;
        }
        catch (RequestException ex) when (ex is not ApiRequestException)
        {
            throw new TelegramDeliveryUnknownException(ex);
        }
    }

    private async Task Answer(CallbackQuery query, CancellationToken cancellation, string? text = default, bool showAlert = false)
    {
        try
        {
            await _botClient.AnswerCallbackQuery(query.Id, text, showAlert, cancellationToken: cancellation);
        }
        catch (ApiRequestException ex)
        {
            // An expired acknowledgement must not retry an already delivered settings panel.
            _logger.LogWarning("Unable to answer settings callback: Telegram error {ErrorCode}", ex.ErrorCode);
        }
    }

    private static string? GetText(string page) => page switch
    {
        "open" => "Личная справка\nПерсональные настройки роли, модели и квоты пока не поддерживаются. Это меню ничего не меняет.",
        "model" => $"Модель обычных ответов: {Models.Gpt_4o_mini}.\nРоль задаётся для всего чата. Выбрать личную модель или роль пока нельзя.",
        "quota" => "Персональная квота в боте не реализована. Строка tokens показывает расход конкретного ответа, а не ваш остаток.",
        _ => null,
    };

    private static InlineKeyboardMarkup Keyboard(long owner, bool openOnly = false) => new(new[]
    {
        openOnly ? new[] { InlineKeyboardButton.WithCallbackData("Справка здесь (только мне)", $"settings:{owner}:open") }
            : new[]
            {
                InlineKeyboardButton.WithCallbackData("Модель и роль", $"settings:{owner}:model"),
                InlineKeyboardButton.WithCallbackData("Квота", $"settings:{owner}:quota"),
            },
    });
}
