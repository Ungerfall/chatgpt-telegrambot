using System;
using System.Collections.Immutable;
using System.Linq;

namespace Ungerfall.ChatGpt.TelegramBot.Configuration;
public class DailyTooLongDidNotReadChats
{
    public virtual ImmutableHashSet<long> Get()
    {
        var chatsEnv = Environment.GetEnvironmentVariable("DailyTooLongDidNotReadChats", EnvironmentVariableTarget.Process)
             ?? throw new ArgumentException("DailyTooLongDidNotReadChats is missing");
        long[] chats = chatsEnv.Split(',').Select(long.Parse).ToArray();

        return ImmutableHashSet.Create(chats);
    }
}
