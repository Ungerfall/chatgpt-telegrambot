using System.Threading.Tasks;
using Telegram.Bot;
using Ungerfall.ChatGpt.TelegramBot.Configuration;

namespace Ungerfall.ChatGpt.TelegramBot.TimedTasks;
public class DailyQuiz(TestUsers testUsers, ITelegramBotClient botClient)
{
    public const string CRON_EXPRESSION = "0 */1 * * * *";

    public async Task Execute()
    {
        var quizChats = testUsers.Get();
        foreach (var chatId in quizChats)
        {
            await botClient.SendPollAsync(
                chatId: chatId,
                question: "Сколько букв в слове яблоко",
                options: ["3", "4", "`6`", "девять"],
                type: Telegram.Bot.Types.Enums.PollType.Quiz,
                correctOptionId: 2,
                explanation: "буква - один символ.",
                explanationParseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
        }
    }
}
