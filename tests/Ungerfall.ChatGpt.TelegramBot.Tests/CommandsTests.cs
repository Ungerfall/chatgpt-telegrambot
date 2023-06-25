using Ungerfall.ChatGpt.TelegramBot.Commands;

namespace Ungerfall.ChatGpt.TelegramBot.Tests;
public class CommandsTests
{
    [Theory]
    [InlineData("/command World", 8)]
    [InlineData("/command@World", 8)]
    [InlineData("/command", 8)]
    public void GetCommandEndIndex_ShouldReturnExpectedIndex(string msg, int expectedIndex)
    {
        // Arrange

        // Act
        var result = msg.GetCommandEndIndex();

        // Assert
        Assert.Equal(expectedIndex, result);
    }
}
