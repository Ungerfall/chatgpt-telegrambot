using System;
using System.Collections.Immutable;
using System.Linq;

namespace Ungerfall.ChatGpt.TelegramBot.Configuration;
public class DailySummaryChats
{
    public virtual ImmutableHashSet<long> Get()
    {
        var chatsEnv = Environment.GetEnvironmentVariable("DailySummaryChats", EnvironmentVariableTarget.Process)
             ?? throw new ArgumentException("DailySummaryChats is missing");
        long[] chats = chatsEnv.Split(',').Select(long.Parse).ToArray();

        return ImmutableHashSet.Create(chats);
    }
}
