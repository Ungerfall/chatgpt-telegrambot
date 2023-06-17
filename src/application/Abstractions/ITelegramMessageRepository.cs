using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ungerfall.ChatGpt.TelegramBot.Database;

namespace Ungerfall.ChatGpt.TelegramBot.Abstractions;
public interface ITelegramMessageRepository
{
    IAsyncEnumerable<TelegramMessage> Get(long chatId, DateOnly dateUtc, CancellationToken cancellation);
    IAsyncEnumerable<TelegramMessage> GetAllOrderByDateDescending(long chatId, CancellationToken cancellation);
    IAsyncEnumerable<TelegramMessage> GetOldMessages(int minLenght, CancellationToken cancellation);
    Task Create(TelegramMessage message, CancellationToken cancellation);
    Task Update(TelegramMessage message, CancellationToken cancellation);
}
