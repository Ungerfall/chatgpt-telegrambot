using System;
using System.Collections.Immutable;
using System.Linq;

namespace Ungerfall.ChatGpt.TelegramBot.Configuration;
public class QuizChats
{
    public ImmutableHashSet<long> Get()
    {
        var quizChatsEnv = Environment.GetEnvironmentVariable("QuizChats", EnvironmentVariableTarget.Process)
             ?? throw new ArgumentException("QuizChats is missing");
        long[] quizChats = quizChatsEnv.Split(',').Select(long.Parse).ToArray();

        return ImmutableHashSet.Create(quizChats);
    }
}
