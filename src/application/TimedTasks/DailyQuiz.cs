using System.Threading.Tasks;
using Telegram.Bot;
using Ungerfall.ChatGpt.TelegramBot.Configuration;

namespace Ungerfall.ChatGpt.TelegramBot.TimedTasks;
public class DailyQuiz(QuizChats quizChats, ITelegramBotClient botClient)
{
    public const string CRON_EXPRESSION = "0 0 6 * * *";

    public async Task Execute()
    {
        foreach (var chatId in quizChats.Get())
        {
            await botClient.SendPollAsync(
                chatId: chatId,
                question: "Что такое HTTP?",
                options:
                [
                    "Hyper Text Markup Language",
                    "Hyperlinks and Text Transfer Protocol",
                    "Home Tool Markup Language",
                    "Hyper Text Transfer Protocol",
                ],
                type: Telegram.Bot.Types.Enums.PollType.Quiz,
                correctOptionId: 3,
                explanation: @"HTTP - это протокол передачи гипертекста, 
используемый для передачи данных веб-страниц между веб-сервером и клиентским браузером.",
                explanationParseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
        }
    }
}
