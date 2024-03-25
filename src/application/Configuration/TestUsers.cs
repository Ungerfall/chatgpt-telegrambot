using System;
using System.Collections.Immutable;
using System.Linq;

namespace Ungerfall.ChatGpt.TelegramBot.Configuration;
public class TestUsers
{
    public ImmutableHashSet<long> Get()
    {
        var testUsersEnv = Environment.GetEnvironmentVariable("TestUsers", EnvironmentVariableTarget.Process)
             ?? throw new ArgumentException("TestUsers is missing");
        long[] testUsers = testUsersEnv.Split(',').Select(long.Parse).ToArray();

        return ImmutableHashSet.Create(testUsers);
    }
}
