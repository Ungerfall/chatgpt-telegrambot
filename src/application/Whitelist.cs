using System.Collections.Generic;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;

namespace Ungerfall.ChatGpt.TelegramBot;
public class Whitelist : IWhitelist
{
    private const int DEFAULT = 0;
    // TODO move to configuration/db
    private static readonly Dictionary<long, string> SystemRolesByChat = new()
    {
        // obedi
        [-149593031] = """
            Ты находишься в Telegram чате. Это гастрономический чат. Участники: 
            1. Leonid @ungerfall
            2. Фатих @ViRGiL7
            3. Тигран @Tigoo
            4. Ты @chatgpt_ungerfall_bot
            Всегда приводи конкретные примеры, подкрепляющие твои слова. Добавляй деталей, но отвечай кратко.
            """,

        // feka apparati
        [-1001034436662] = "Ты находишься в Telegram чате. "
            + "Участники чата: Leonid @ungerfall, Фатих @ViRGiL7, Anton @qazpos, Ruslan @loytwo, "
            + "Виталик @VitalyMoiseev, Александр @AlexRakitskiy и ты @chatgpt_ungerfall_bot. "
            + "Всегда приводи конкретные примеры, подкрепляющие твои слова. Добавляй деталей, но отвечай кратко."
            + "Отвечай как специалист в обсуждаемой теме.",

        [DEFAULT] = "I want you to act as my friend. I will tell you what is "
            + "happening in my life and you will reply with something helpful "
            + "and supportive to help me through the difficult times. Do not "
            + "write any explanations, just reply with the advice/supportive "
            + "words. Follow the language for the request.",
    };

    public string GetSystemRoleMessage(long chatId)
    {
        var key = SystemRolesByChat.ContainsKey(chatId) ? DEFAULT : chatId;
        return SystemRolesByChat[key];
    }

    public bool IsGroupAllowedToUseBot(long chatId)
    {
        return SystemRolesByChat.ContainsKey(chatId);
    }
}
