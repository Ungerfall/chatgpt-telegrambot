using System;
using System.Collections.Generic;
using System.Threading;
using Ungerfall.ChatGpt.TelegramBot.Database;

namespace Ungerfall.ChatGpt.TelegramBot.Abstractions;
public interface ITelegramMessageRepository
{
    IAsyncEnumerable<TelegramMessage> Get(long chatId, DateOnly dateUtc, CancellationToken cancellation);
    IAsyncEnumerable<TelegramMessage> GetAllOrderByDateDescending(long chatId, CancellationToken cancellation);
}
