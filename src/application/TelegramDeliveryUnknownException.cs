using System;
using Telegram.Bot.Exceptions;

namespace Ungerfall.ChatGpt.TelegramBot;
public sealed class TelegramDeliveryUnknownException : Exception
{
    public TelegramDeliveryUnknownException(RequestException innerException)
        : base("Telegram delivery could not be confirmed; automatic resend is unsafe.", innerException)
    {
    }
}
