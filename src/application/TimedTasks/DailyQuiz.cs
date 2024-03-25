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
                question: "Какая из этих сортировок является примером алгоритма сортировки слиянием?",
                options:
                [
                    "Пирамидальная сортировка",
                    "Сортировка Шелла",
                    "Быстрая сортировка",
                    "Сортировка выбором",
                    "Сортировка вставками",
                ],
                type: Telegram.Bot.Types.Enums.PollType.Quiz,
                correctOptionId: 4,
                explanation: @"Сортировка вставками (Insertion Sort) эффективна для небольших данных или когда большая 
часть элементов списка уже упорядочена. Это связано с тем, что она проходит через список, оставляя упорядоченную 
последовательность за собой, и эффективно работает с почти упорядоченными данными, 
минимизируя количество необходимых перемещений.",
                explanationParseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
        }
    }
}
