using System;
using Telegram.Bot.Exceptions;

namespace Ungerfall.ChatGpt.TelegramBot;
public sealed class TelegramDeliveryRejectedException : Exception
{
    public TelegramDeliveryRejectedException(ApiRequestException innerException)
        : base("Telegram rejected the first send; no response fragment was delivered.", innerException)
    {
    }

    internal static bool IsDefiniteRejection(ApiRequestException exception) => exception.ErrorCode is 400 or 401 or 403 or 404 or 429;
}
