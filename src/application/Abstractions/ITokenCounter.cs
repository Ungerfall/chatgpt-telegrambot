namespace Ungerfall.ChatGpt.TelegramBot.Abstractions;
public interface ITokenCounter
{
    int Count(string text);
}
