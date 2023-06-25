namespace Ungerfall.ChatGpt.TelegramBot.Commands;
public static class CommandsExtensions
{
    public static int GetCommandEndIndex(this string msg)
    {
        for (int i = 1; i < msg.Length; i++)
        {
            if (msg[i] == ' ')
                return i;
            if (msg[i] == '@')
                return i;
        }

        return msg.Length;
    }
}
