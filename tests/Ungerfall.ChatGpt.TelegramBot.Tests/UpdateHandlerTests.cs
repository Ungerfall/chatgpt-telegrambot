using Microsoft.Extensions.Logging;
using OpenAI.Interfaces;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;
using OpenAI.ObjectModels.SharedModels;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using Ungerfall.ChatGpt.TelegramBot.Commands;
using Ungerfall.ChatGpt.TelegramBot.Configuration;
using Ungerfall.ChatGpt.TelegramBot.Database;

namespace Ungerfall.ChatGpt.TelegramBot.Tests;
public class UpdateHandlerTest
{
    [Fact]
    public async Task Handle_ShouldAwaitHistoryAndPropagateError_WhenDatabaseWriteFails()
    {
        // Arrange
        const long chatId = 123;
        var botClientMock = Substitute.For<ITelegramBotClient>();
        var loggerMock = Substitute.For<ILogger<UpdateHandler>>();
        var openAiServiceMock = Substitute.For<IOpenAIService>();
        var messageRepoMock = Substitute.For<ITelegramMessageRepository>();
        var tokenCounterMock = Substitute.For<ITokenCounter>();
        var whitelistMock = Substitute.For<IWhitelist>();
        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var error = new InvalidOperationException("Database unavailable");
        var update = new Update
        {
            Message = new Message
            {
                MessageId = 1,
                Chat = new Chat { Id = chatId, Type = ChatType.Group },
                Text = "Hello",
            }
        };

        whitelistMock.IsGroupAllowedToUseBot(chatId).Returns(true);
        messageRepoMock.Create(Arg.Any<TelegramMessage>(), Arg.Any<CancellationToken>())
            .Returns(saved.Task);

        var handler = new UpdateHandler(
            botClientMock, loggerMock, openAiServiceMock, messageRepoMock, tokenCounterMock,
            new TooLongDidNotReadToday(messageRepoMock, tokenCounterMock, openAiServiceMock,
                Substitute.For<ILogger<TooLongDidNotReadToday>>(), botClientMock, whitelistMock),
            whitelistMock,
            new GenerateImage(openAiServiceMock, Substitute.For<ILogger<GenerateImage>>(), botClientMock),
            new TestUsers());

        // Act
        var handling = handler.Handle(update, default);
        var completedBeforeSave = handling.IsCompleted;
        saved.SetException(error);
        var exception = await Record.ExceptionAsync(() => handling);

        // Assert
        Assert.False(completedBeforeSave);
        Assert.Same(error, exception);
        await messageRepoMock.Received(1).Create(Arg.Any<TelegramMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRestoreBotResponseAsAssistant_WhenHistoryIsRead()
    {
        // Arrange
        const long chatId = 123;
        var botClientMock = Substitute.For<ITelegramBotClient>();
        var loggerMock = Substitute.For<ILogger<UpdateHandler>>();
        var openAiServiceMock = Substitute.For<IOpenAIService>();
        var messageRepoMock = Substitute.For<ITelegramMessageRepository>();
        var tokenCounterMock = Substitute.For<ITokenCounter>();
        var whitelistMock = Substitute.For<IWhitelist>();
        var messages = new List<TelegramMessage>();
        var chat = new Chat { Id = chatId, Type = ChatType.Private };
        var sent = new Message
        {
            MessageId = 2,
            Chat = chat,
            Date = DateTime.UtcNow,
            From = new User { Id = 456, IsBot = true, FirstName = "Bot", Username = "chatgpt_ungerfall_bot" },
        };
        var update = new Update
        {
            Message = new Message
            {
                MessageId = 1,
                Chat = chat,
                Text = "Hello",
                Date = DateTime.UtcNow,
                From = new User { Id = 789, FirstName = "User" },
            }
        };

        whitelistMock.IsGroupAllowedToUseBot(chatId).Returns(true);
        whitelistMock.GetSystemRoleMessage(chatId).Returns("System role");
        messageRepoMock.GetAllOrderByDateDescending(chatId, Arg.Any<CancellationToken>())
            .Returns(_ => messages.AsEnumerable().Reverse().ToAsyncEnumerable());
        messageRepoMock.Create(Arg.Do<TelegramMessage>(messages.Add), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        openAiServiceMock.ChatCompletion.CreateCompletion(
                Arg.Any<ChatCompletionCreateRequest>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatCompletionCreateResponse
            {
                Choices = [new ChatChoiceResponse { Message = ChatMessage.FromAssistant("Assistant reply") }]
            });
        botClientMock.MakeRequestAsync<Message>(Arg.Any<SendMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(sent);

        var handler = new UpdateHandler(
            botClientMock, loggerMock, openAiServiceMock, messageRepoMock, tokenCounterMock,
            new TooLongDidNotReadToday(messageRepoMock, tokenCounterMock, openAiServiceMock,
                Substitute.For<ILogger<TooLongDidNotReadToday>>(), botClientMock, whitelistMock),
            whitelistMock,
            new GenerateImage(openAiServiceMock, Substitute.For<ILogger<GenerateImage>>(), botClientMock),
            new TestUsers());

        // Act
        await handler.Handle(update, default);
        var history = new ConversationHistory(messageRepoMock, "Next message", tokenCounterMock, whitelistMock);
        var (gptMessages, _) = await history.GetForChatGpt(chatId, default);

        // Assert
        var botMessage = Assert.Single(messages, m => m.MessageId == sent.MessageId);
        Assert.Equal("chatgpt_ungerfall_bot", botMessage.User);
        Assert.Equal(sent.From.Id, botMessage.UserId);
        var assistant = Assert.Single(gptMessages, m => m.Role == StaticValues.ChatMessageRoles.Assistant);
        Assert.Equal("Assistant reply", assistant.Content);
    }
}
