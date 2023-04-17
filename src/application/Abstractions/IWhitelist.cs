namespace Ungerfall.ChatGpt.TelegramBot.Abstractions;
public interface IWhitelist
{
    bool IsGroupAllowedToUseBot(long chatId);
    string GetSystemRoleMessage(long chatId);
}
