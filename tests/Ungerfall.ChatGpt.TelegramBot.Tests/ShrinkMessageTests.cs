using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OpenAI.Interfaces;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;
using OpenAI.ObjectModels.SharedModels;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using Ungerfall.ChatGpt.TelegramBot.AzureFunction;
using Ungerfall.ChatGpt.TelegramBot.Database;

namespace Ungerfall.ChatGpt.TelegramBot.Tests;
public class ShrinkMessageTest
{
    [Fact]
    public async Task Run_ShouldShrinkMessage_WhenMessageIsOldAndLengthy()
    {
        // Arrange
        var loggerMock = Substitute.For<ILogger<ShrinkMessage>>();
        var openAiServiceMock = Substitute.For<IOpenAIService>();
        var tokenCounterMock = Substitute.For<ITokenCounter>();
        var messageRepoMock = Substitute.For<ITelegramMessageRepository>();

        var oldMessage = new TelegramMessage
        {
            Id = Guid.NewGuid().ToString(),
            Message = @"This is a really long message that needs to be shrunk. 
                It is extremely verbose and contains a lot of unnecessary words.",
            IsShrunk = false,
            DateUtc = DateTime.UtcNow.AddDays(-10).ToString(TelegramMessage.DATE_UTC_FORMAT),
        };

        messageRepoMock.GetOldMessages(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<TelegramMessage> { oldMessage }.ToAsyncEnumerable());

        tokenCounterMock.Count(oldMessage.Message).Returns(25);
        tokenCounterMock.Count(Arg.Is<string>(p => !p.Equals(oldMessage.Message))).Returns(10);

        openAiServiceMock.ChatCompletion.CreateCompletion(
                Arg.Any<ChatCompletionCreateRequest>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatCompletionCreateResponse
            {
                Choices =
                [
                    new ChatChoiceResponse
                    {
                        Message = ChatMessage.FromAssistant("Shrunk message")
                    }
                ]
            });

        var function = new ShrinkMessage(
            openAiServiceMock,
            tokenCounterMock,
            loggerMock,
            messageRepoMock);

        // Act
        await function.Run(new TimerInfo { ScheduleStatus = default, IsPastDue = default });

        // Assert
        await messageRepoMock.Received().Update(
                Arg.Is<TelegramMessage>(m => m.IsShrunk && m.Message == "Shrunk message"),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_ShouldNotShrinkMessage_WhenChatGPTRequestIsUnsuccessful()
    {
        // Arrange
        var loggerMock = Substitute.For<ILogger<ShrinkMessage>>();
        var openAiServiceMock = Substitute.For<IOpenAIService>();
        var tokenCounterMock = Substitute.For<ITokenCounter>();
        var messageRepoMock = Substitute.For<ITelegramMessageRepository>();

        var oldMessage = new TelegramMessage
        {
            Id = Guid.NewGuid().ToString(),
            Message = @"This is a really long message that needs to be shrunk. 
                It is extremely verbose and contains a lot of unnecessary words.",
            IsShrunk = false,
            DateUtc = DateTime.UtcNow.AddDays(-10).ToString(TelegramMessage.DATE_UTC_FORMAT),
        };

        messageRepoMock.GetOldMessages(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<TelegramMessage> { oldMessage }.ToAsyncEnumerable());

        tokenCounterMock.Count(oldMessage.Message).Returns(25);

        openAiServiceMock.ChatCompletion.CreateCompletion(
                Arg.Any<ChatCompletionCreateRequest>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatCompletionCreateResponse
            {
                Error = new Error(),
            });

        var function = new ShrinkMessage(
            openAiServiceMock,
            tokenCounterMock,
            loggerMock,
            messageRepoMock);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            function.Run(new TimerInfo { ScheduleStatus = default, IsPastDue = default }));
    }

    [Fact]
    public async Task Run_ShouldNotUpdate_WhenShrunkMessageIsLong()
    {
        // Arrange
        var loggerMock = Substitute.For<ILogger<ShrinkMessage>>();
        var openAiServiceMock = Substitute.For<IOpenAIService>();
        var tokenCounterMock = Substitute.For<ITokenCounter>();
        var messageRepoMock = Substitute.For<ITelegramMessageRepository>();

        var oldMessage = new TelegramMessage
        {
            Id = Guid.NewGuid().ToString(),
            Message = "This is a somewhat lengthy message.",
            IsShrunk = false,
            DateUtc = DateTime.UtcNow.AddDays(-10).ToString(TelegramMessage.DATE_UTC_FORMAT),
        };

        messageRepoMock.GetOldMessages(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<TelegramMessage> { oldMessage }.ToAsyncEnumerable());

        tokenCounterMock.Count(oldMessage.Message).Returns(6);
        tokenCounterMock.Count(Arg.Is<string>(p => !p.Equals(oldMessage.Message))).Returns(10);

        openAiServiceMock.ChatCompletion.CreateCompletion(
                Arg.Any<ChatCompletionCreateRequest>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatCompletionCreateResponse
            {
                Choices =
                [
                    new ChatChoiceResponse
                    {
                        Message = ChatMessage.FromAssistant("This is an even longer message than before!")
                    }
                ]
            });

        var function = new ShrinkMessage(
            openAiServiceMock,
            tokenCounterMock,
            loggerMock,
            messageRepoMock);

        // Act
        await function.Run(new TimerInfo { ScheduleStatus = default, IsPastDue = default });

        // Assert
        await messageRepoMock.Received(requiredNumberOfCalls: 0)
            .Update(Arg.Any<TelegramMessage>(), Arg.Any<CancellationToken>());
    }
}
