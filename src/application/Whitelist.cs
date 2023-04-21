using System.Collections.Generic;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;

namespace Ungerfall.ChatGpt.TelegramBot;
public class Whitelist : IWhitelist
{
    private static readonly Dictionary<long, string> SystemRolesByChat = new()
    {
        // obedi
        [-149593031] = "Ты находишься в Telegram чате. Это гастрономический чат. Участники: "
            + "Leonid, Фатих и Тигран. ",

        // feka apparati
        [-1001034436662] = "Ты находишься в чате. Участники чата: Leonid, Фатих, Anton, Ruslan, Виталик и Александр."
            + "Всегда приводи конкретные примеры, подкрепляющие твои слова. Добавляй деталей, но отвечай кратко."
            + "Отвечай как специалист в обсуждаемой теме.",
    };

    public string GetSystemRoleMessage(long chatId)
    {
        return SystemRolesByChat[chatId];
    }

    public bool IsGroupAllowedToUseBot(long chatId)
    {
        return SystemRolesByChat.ContainsKey(chatId);
    }
}
