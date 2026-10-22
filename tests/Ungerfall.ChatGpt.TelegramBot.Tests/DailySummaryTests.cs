using Microsoft.Extensions.Logging;
using OpenAI.Interfaces;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;
using OpenAI.ObjectModels.SharedModels;
using System.Collections.Immutable;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using Ungerfall.ChatGpt.TelegramBot.Configuration;
using Ungerfall.ChatGpt.TelegramBot.Database;
using Ungerfall.ChatGpt.TelegramBot.TimedTasks;

namespace Ungerfall.ChatGpt.TelegramBot.Tests;
public class DailySummaryTest
{
    [Fact]
    public async Task Execute_ShouldNotRepeatHistory_WhenMessagesRequireMultipleBatches()
    {
        // Arrange
        const long chatId = 123;
        var chatsMock = Substitute.For<DailySummaryChats>();
        var botClientMock = Substitute.For<ITelegramBotClient>();
        var executionRepoMock = Substitute.For<ITimedTaskExecutionRepository>();
        var loggerMock = Substitute.For<ILogger<DailySummary>>();
        var whitelistMock = Substitute.For<IWhitelist>();
        var messageRepoMock = Substitute.For<ITelegramMessageRepository>();
        var openAiServiceMock = Substitute.For<IOpenAIService>();
        var requests = new List<ChatCompletionCreateRequest>();
        var messages = Enumerable.Range(1, 3).Select(i => new TelegramMessage
        {
            User = "User",
            Message = $"{i}: {string.Join(' ', Enumerable.Repeat("word", 2000))}",
        }).ToList();

        chatsMock.Get().Returns(ImmutableHashSet.Create(chatId));
        whitelistMock.GetSystemRoleMessage(chatId).Returns("System role");
        messageRepoMock.Get(chatId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(messages.ToAsyncEnumerable());
        openAiServiceMock.ChatCompletion.CreateCompletion(
                Arg.Do<ChatCompletionCreateRequest>(requests.Add),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatCompletionCreateResponse
            {
                Choices = [new ChatChoiceResponse { Message = ChatMessage.FromAssistant("Summary") }]
            });

        var task = new DailySummary(
            chatsMock, botClientMock, executionRepoMock, loggerMock,
            whitelistMock, messageRepoMock, openAiServiceMock);

        // Act
        await task.Execute();

        // Assert
        Assert.Equal(messages.Count, requests.Count);
        Assert.All(requests, request =>
        {
            Assert.Equal("System role", request.Messages.First().Content);
            Assert.Equal(3, request.Messages.Count);
        });
        foreach (var message in messages)
        {
            Assert.Single(requests.SelectMany(r => r.Messages),
                m => m.Content == $"{message.User}: {message.Message}");
        }
        await executionRepoMock.Received().Create(Arg.Any<TimedTaskExecution>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ShouldSplitSummaryWithoutLosingText_WhenEmojiCrossesMessageLimit()
    {
        // Arrange
        const long chatId = 123;
        var chatsMock = Substitute.For<DailySummaryChats>();
        var botClientMock = Substitute.For<ITelegramBotClient>();
        var executionRepoMock = Substitute.For<ITimedTaskExecutionRepository>();
        var loggerMock = Substitute.For<ILogger<DailySummary>>();
        var whitelistMock = Substitute.For<IWhitelist>();
        var messageRepoMock = Substitute.For<ITelegramMessageRepository>();
        var openAiServiceMock = Substitute.For<IOpenAIService>();
        var sentMessages = new List<SendMessageRequest>();
        var summary = "*" + new string('a', 4094) + "😀" + new string('b', 4100);

        chatsMock.Get().Returns(ImmutableHashSet.Create(chatId));
        whitelistMock.GetSystemRoleMessage(chatId).Returns("System role");
        messageRepoMock.Get(chatId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<TelegramMessage>
            {
                new() { User = "User", Message = "Hello" }
            }.ToAsyncEnumerable());
        openAiServiceMock.ChatCompletion.CreateCompletion(
                Arg.Any<ChatCompletionCreateRequest>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatCompletionCreateResponse
            {
                Choices = [new ChatChoiceResponse { Message = ChatMessage.FromAssistant(summary) }]
            });
        botClientMock.SendRequest<Message>(
                Arg.Do<SendMessageRequest>(sentMessages.Add),
                Arg.Any<CancellationToken>())
            .Returns(new Message());

        var task = new DailySummary(
            chatsMock, botClientMock, executionRepoMock, loggerMock,
            whitelistMock, messageRepoMock, openAiServiceMock);

        // Act
        await task.Execute();

        // Assert
        Assert.True(sentMessages.Count > 1);
        Assert.Equal(summary, string.Concat(sentMessages.Select(m => m.Text)));
        Assert.All(sentMessages, message =>
        {
            Assert.Equal(chatId, message.ChatId.Identifier);
            Assert.InRange(message.Text.Length, 1, 4096);
            Assert.False(char.IsHighSurrogate(message.Text[^1]));
            Assert.False(char.IsLowSurrogate(message.Text[0]));
            Assert.Equal(ParseMode.None, message.ParseMode);
        });
        await executionRepoMock.Received().Create(Arg.Any<TimedTaskExecution>(), Arg.Any<CancellationToken>());
    }
}
