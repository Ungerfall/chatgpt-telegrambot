using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OpenAI.GPT3.Interfaces;
using OpenAI.GPT3.ObjectModels.RequestModels;
using System;
using System.Threading.Tasks;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using Ungerfall.ChatGpt.TelegramBot.Queue;

namespace Ungerfall.ChatGpt.TelegramBot.AzureFunction;

/// <summary>
/// Shorten old messages using Chat GPT
/// </summary>
public class BriefMessage
{
    private const int MIN_TOKENS_COUNT = 100;

    private readonly ILogger _logger;
    private readonly IOpenAIService _openAiService;
    private readonly ITokenCounter _tokenCounter;

    public BriefMessage(ILogger<BriefMessage> logger, IOpenAIService openAiService, ITokenCounter tokenCounter)
    {
        _logger = logger;
        _openAiService = openAiService;
        _tokenCounter = tokenCounter;
    }

    [Function("BriefMessage")]
    /*
    [CosmosDBOutput(databaseName: "%CosmosDatabase%",
        containerName: "%CosmosTelegramMessagesContainer%",
        Connection = "CosmosDbConnectionString",
        CreateIfNotExists = true)]
    */
    public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo timer)
    {
        _logger.LogInformation("C# Timer trigger function executed at: {now}", DateTime.Now);
        _logger.LogInformation("Next timer schedule at: {nextSchedule}", timer?.ScheduleStatus?.Next);
    }
    /*
    public async Task<TelegramMessage> Run([ServiceBusTrigger(QueueTelegramMessage.QUEUE_NAME, Connection = "ServiceBusConnection")] QueueTelegramMessage msg)
    {
        using var fs = new FileStream("123", FileAccess.Read);
        _logger.LogInformation("C# ServiceBus queue trigger function processed message: {msg}", msg.Message);
        var tokensCount = _tokenCounter.Count(msg.Message);
        if (tokensCount <= MIN_TOKENS_COUNT)
        {
            briefMsg.Message = msg.Message;
        }
        else
        {
            var gpt = await AskChatGptForBriefMessage(msg);
            var gptTokensCount = _tokenCounter.Count(gpt);
            // if gpt version is longer keep original version
            briefMsg.Message = gptTokensCount >= tokensCount
                ? msg.Message
                : briefMsg.Message = gpt;
        }

        return briefMsg;
    }
    */

    private async Task<string> AskChatGptForBriefMessage(QueueTelegramMessage msg)
    {
        var completionResult = await _openAiService.ChatCompletion.CreateCompletion(
            new ChatCompletionCreateRequest
            {
                Messages = new[]
                {
                    ChatMessage.FromSystem("Вы — искусственный интеллект, который уменьшает количество токенов истории чата Телеграм."),
                    ChatMessage.FromUser("Сделай коротко: " + msg.Message),
                },
                Temperature = 0f,
                User = msg.User,
            });
        if (completionResult.Successful)
        {
            return completionResult.Choices[0].Message.Content;
        }
        else
        {
            throw new InvalidOperationException("ChatGPT request wasn't successful");
        }
    }
}
