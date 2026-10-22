using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace Ungerfall.ChatGpt.TelegramBot;
public static class TelegramBotClientExtensions
{
    public static async Task<Message> SendResponse(
        this ITelegramBotClient botClient,
        long chatId,
        string text,
        bool useRich = false,
        ReplyParameters? replyParameters = default,
        int? messageThreadId = default,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await SendResponseCore(botClient, chatId, text, useRich, replyParameters, messageThreadId, cancellationToken);
        }
        catch (RequestException ex) when (ex is not ApiRequestException)
        {
            throw new TelegramDeliveryUnknownException(ex);
        }
    }

    private static async Task<Message> SendResponseCore(
        ITelegramBotClient botClient,
        long chatId,
        string text,
        bool useRich,
        ReplyParameters? replyParameters,
        int? messageThreadId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        const string detailsTitle = "Подробнее";
        if (useRich && Encoding.UTF8.GetByteCount(text) + Encoding.UTF8.GetByteCount(detailsTitle) <= 32768)
        {
            var previewLength = Math.Min(600, text.Length);
            var lineEnd = text.IndexOf('\n');
            if (lineEnd > 0 && lineEnd < previewLength)
            {
                previewLength = lineEnd;
            }
            if (previewLength < text.Length
                && char.IsHighSurrogate(text[previewLength - 1])
                && char.IsLowSurrogate(text[previewLength]))
            {
                previewLength--;
            }

            var blocks = new List<InputRichBlock>
            {
                new InputRichBlockParagraph { Text = text[..previewLength] }
            };
            if (previewLength < text.Length)
            {
                blocks.Add(new InputRichBlockDetails
                {
                    Summary = detailsTitle,
                    Blocks = new InputRichBlock[]
                    {
                        new InputRichBlockParagraph { Text = text[previewLength..] }
                    },
                });
            }

            try
            {
                return await botClient.SendRichMessage(
                    chatId: chatId,
                    richMessage: new InputRichMessage { Blocks = blocks },
                    replyParameters: replyParameters,
                    messageThreadId: messageThreadId,
                    cancellationToken: cancellationToken);
            }
            catch (ApiRequestException ex) when (ex.ErrorCode is 400 or 404)
            {
                // Only an explicit API rejection allows a fallback; a timeout must not trigger another send.
            }
            catch (ApiRequestException ex) when (TelegramDeliveryRejectedException.IsDefiniteRejection(ex))
            {
                throw new TelegramDeliveryRejectedException(ex);
            }
        }

        Message? first = default;
        const int maxMessageLength = 4096;
        for (var offset = 0; offset < text.Length;)
        {
            var length = Math.Min(maxMessageLength, text.Length - offset);
            if (offset + length < text.Length
                && char.IsHighSurrogate(text[offset + length - 1])
                && char.IsLowSurrogate(text[offset + length]))
            {
                length--;
            }

            Message sent;
            try
            {
                sent = await botClient.SendMessage(
                    chatId: chatId,
                    text: text.Substring(offset, length),
                    replyParameters: replyParameters,
                    messageThreadId: messageThreadId,
                    cancellationToken: cancellationToken);
            }
            catch (ApiRequestException ex) when (first == null && TelegramDeliveryRejectedException.IsDefiniteRejection(ex))
            {
                throw new TelegramDeliveryRejectedException(ex);
            }
            first ??= sent;
            offset += length;
        }
        return first!;
    }
}
