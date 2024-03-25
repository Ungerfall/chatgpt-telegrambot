using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Moq;
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
        var loggerMock = new Mock<ILogger<ShrinkMessage>>();
        var openAiServiceMock = new Mock<IOpenAIService>();
        var tokenCounterMock = new Mock<ITokenCounter>();
        var messageRepoMock = new Mock<ITelegramMessageRepository>();

        var oldMessage = new TelegramMessage
        {
            Id = Guid.NewGuid().ToString(),
            Message = @"This is a really long message that needs to be shrunk. 
                It is extremely verbose and contains a lot of unnecessary words.",
            IsShrunk = false,
            DateUtc = DateTime.UtcNow.AddDays(-10).ToString(TelegramMessage.DATE_UTC_FORMAT),
        };

        messageRepoMock.Setup(repo => repo.GetOldMessages(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(new List<TelegramMessage> { oldMessage }.ToAsyncEnumerable());

        tokenCounterMock.Setup(tc => tc.Count(oldMessage.Message)).Returns(25);
        tokenCounterMock.Setup(tc => tc.Count(It.IsNotIn(oldMessage.Message))).Returns(10);

        openAiServiceMock.Setup(service => service.ChatCompletion.CreateCompletion(
                It.IsAny<ChatCompletionCreateRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatCompletionCreateResponse
            {
                Choices = new List<ChatChoiceResponse>
                {
                    new ChatChoiceResponse
                    {
                        Message = ChatMessage.FromAssistant("Shrunk message")
                    }
                }
            });

        var function = new ShrinkMessage(
            openAiServiceMock.Object,
            tokenCounterMock.Object,
            loggerMock.Object,
            messageRepoMock.Object);

        // Act
        await function.Run(new TimerInfo { ScheduleStatus = default, IsPastDue = default });

        // Assert
        messageRepoMock.Verify(
            repo => repo.Update(
                It.Is<TelegramMessage>(m => m.IsShrunk && m.Message == "Shrunk message"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Run_ShouldNotShrinkMessage_WhenChatGPTRequestIsUnsuccessful()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<ShrinkMessage>>();
        var openAiServiceMock = new Mock<IOpenAIService>();
        var tokenCounterMock = new Mock<ITokenCounter>();
        var messageRepoMock = new Mock<ITelegramMessageRepository>();

        var oldMessage = new TelegramMessage
        {
            Id = Guid.NewGuid().ToString(),
            Message = @"This is a really long message that needs to be shrunk. 
                It is extremely verbose and contains a lot of unnecessary words.",
            IsShrunk = false,
            DateUtc = DateTime.UtcNow.AddDays(-10).ToString(TelegramMessage.DATE_UTC_FORMAT),
        };

        messageRepoMock.Setup(repo => repo.GetOldMessages(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(new List<TelegramMessage> { oldMessage }.ToAsyncEnumerable());

        tokenCounterMock.Setup(tc => tc.Count(oldMessage.Message)).Returns(25);

        openAiServiceMock.Setup(service => service.ChatCompletion.CreateCompletion(
                It.IsAny<ChatCompletionCreateRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatCompletionCreateResponse
            {
                Error = new Error(),
            });

        var function = new ShrinkMessage(
            openAiServiceMock.Object,
            tokenCounterMock.Object,
            loggerMock.Object,
            messageRepoMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            function.Run(new TimerInfo { ScheduleStatus = default, IsPastDue = default }));
    }

    [Fact]
    public async Task Run_ShouldNotUpdate_WhenShrunkMessageIsLong()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<ShrinkMessage>>();
        var openAiServiceMock = new Mock<IOpenAIService>();
        var tokenCounterMock = new Mock<ITokenCounter>();
        var messageRepoMock = new Mock<ITelegramMessageRepository>();

        var oldMessage = new TelegramMessage
        {
            Id = Guid.NewGuid().ToString(),
            Message = "This is a somewhat lengthy message.",
            IsShrunk = false,
            DateUtc = DateTime.UtcNow.AddDays(-10).ToString(TelegramMessage.DATE_UTC_FORMAT),
        };

        messageRepoMock.Setup(repo => repo.GetOldMessages(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(new List<TelegramMessage> { oldMessage }.ToAsyncEnumerable());

        tokenCounterMock.Setup(tc => tc.Count(oldMessage.Message)).Returns(6);
        tokenCounterMock.Setup(tc => tc.Count(It.IsNotIn(oldMessage.Message))).Returns(10);

        openAiServiceMock.Setup(service => service.ChatCompletion.CreateCompletion(
                It.IsAny<ChatCompletionCreateRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatCompletionCreateResponse
            {
                Choices = new List<ChatChoiceResponse>
                {
                    new ChatChoiceResponse
                    {
                        Message = ChatMessage.FromAssistant("This is an even longer message than before!")
                    }
                }
            });

        var function = new ShrinkMessage(
            openAiServiceMock.Object,
            tokenCounterMock.Object,
            loggerMock.Object,
            messageRepoMock.Object);

        // Act
        await function.Run(new TimerInfo { ScheduleStatus = default, IsPastDue = default });

        // Assert
        messageRepoMock.Verify(
            repo => repo.Update(It.IsAny<TelegramMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
