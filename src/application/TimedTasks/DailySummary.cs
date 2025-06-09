using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using Telegram.Bot;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using Ungerfall.ChatGpt.TelegramBot.AzureOpenAi;
using Ungerfall.ChatGpt.TelegramBot.Configuration;

namespace Ungerfall.ChatGpt.TelegramBot.TimedTasks;
public class DailySummary(
    DailySummaryChats chats,
    ITelegramBotClient botClient,
    ITimedTaskExecutionRepository repo,
    ILogger<DailySummary> logger,
    IWhitelist whitelist,
    ITelegramMessageRepository messages,
    AzureOpenAIClient azureOpenAiClient) : TimedTask(repo, botClient, logger)
{
    public const string CRON_EXPRESSION = "0 55 23 * * *";

    // TODO: move to config/db
    private const string Prompt = """
            Напиши краткое содержание переписки.
            Если есть неотвеченные вопросы, перечисли их и ответь.
            В конце с новой строки добавь самые релевантные #теги переписки до 5 штук.
            """;

    private readonly ChatClient _chat = azureOpenAiClient.GetChatClient(Deployments.Gpt_4_1);

    protected override IEnumerable<long> ChatIds => chats.Get();
    protected override string Name => "Daily summary";
    protected override DateTime Date => DateTime.UtcNow;
    protected override string Type => "daily-summary";

    protected override async Task ExecuteForChat(long chatId)
    {
        var gptTasks = new List<Task<string>>();
        await foreach (var r in CreateGptRequestBatches(chatId, cancellation: default))
        {
            gptTasks.Add(SendGptRequest(r, cancellation: default));
        }

        var summaries = await Task.WhenAll(gptTasks);
        var telegramMessage = summaries.Length == 0
            ? "Сегодня ничего не произошло"
            : string.Join(Environment.NewLine, summaries);
        await _botClient.SendMessage(
            chatId: chatId,
            text: telegramMessage,
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
            cancellationToken: default);
    }

    private async IAsyncEnumerable<ChatMessage[]> CreateGptRequestBatches(
        long chatId,
        [EnumeratorCancellation] CancellationToken cancellation = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var builder = ChatMessageBuilder.Create()
            .WithTokenCounter()
            .WithSystemRoleMessage(whitelist.GetSystemRoleMessage(chatId));
        await foreach (var h in messages.Get(chatId, today, cancellation))
        {
            // hit tokens limit
            if (!builder.CanAddMessage)
            {
                builder.AddUserMessage(Prompt);
                yield return builder.Build();
            }

            builder.AddMessage(h, 1); // descending order of items in history
        }

        if (!builder.ContainsUserMessage)
        {
            yield break;
        }

        builder.AddUserMessage(Prompt);
        yield return builder.Build();
    }

    private async Task<string> SendGptRequest(IEnumerable<ChatMessage> messages, CancellationToken cancellation = default)
    {
        ChatCompletion completionResult = await _chat.CompleteChatAsync(messages, cancellationToken: cancellation);
        string result = completionResult.FinishReason switch
        {
            ChatFinishReason.Stop => completionResult.Content[0].Text,
            ChatFinishReason.Length => completionResult.Content[0].Text + Environment.NewLine + "Finish reason: length",
            ChatFinishReason.ContentFilter => "Finish reason: Content Filter. Refusal: " + completionResult.Refusal,
            _ => $"Finish reason: {completionResult.FinishReason}",
        };

        return result;
    }
}
