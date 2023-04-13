using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OpenAI.GPT3.Interfaces;
using OpenAI.GPT3.ObjectModels.RequestModels;
using System;
using System.Threading.Tasks;
using Ungerfall.ChatGpt.TelegramBot;
using Ungerfall.ChatGpt.TelegramBot.Database;
using Ungerfall.ChatGpt.TelegramBot.Queue;

namespace azure_function;

public class BriefMessage
{
    private const int MIN_TOKENS_COUNT = 100;
    private const int TTL = 1 * 24 * 60 * 60;

    private readonly ILogger _logger;
    private readonly IOpenAIService _openAiService;
    private readonly TokenCounter _tokenCounter;

    public BriefMessage(ILogger<BriefMessage> logger, IOpenAIService openAiService, TokenCounter tokenCounter)
    {
        _logger = logger;
        _openAiService = openAiService;
        _tokenCounter = tokenCounter;
    }

    [Function("BriefMessage")]
    [CosmosDBOutput(databaseName: "%CosmosDatabase%",
        containerName: "%CosmosTelegramMessagesContainer%",
        Connection = "CosmosDbConnectionString",
        CreateIfNotExists = true)]
    public async Task<BriefTelegramMessage> Run([ServiceBusTrigger(QueueTelegramMessage.QUEUE_NAME, Connection = "ServiceBusConnection")] QueueTelegramMessage msg)
    {
        _logger.LogInformation("C# ServiceBus queue trigger function processed message: {msg}", msg.Message);
        var date = DateOnly.FromDateTime(DateTime.UtcNow).ToString(BriefTelegramMessage.DATE_UTC_FORMAT);
        var tokensCount = _tokenCounter.Count(msg.Message);
        var briefMsg = new BriefTelegramMessage
        {
            Id = Guid.NewGuid(),
            User = msg.User,
            UserId = msg.UserId,
            MessageId = msg.MessageId,
            Date = msg.Date,
            DateUtc = date,
            TTL = TTL
        };

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

    private async Task<string> AskChatGptForBriefMessage(QueueTelegramMessage msg)
    {
        var completionResult = await _openAiService.ChatCompletion.CreateCompletion(
            new ChatCompletionCreateRequest
            {
                Messages = new[]
                {
                    ChatMessage.FromSystem("Вы — искусственный интеллект, дающий краткие и лаконичные ответы."),
                    ChatMessage.FromUser("Сделай коротко и лаконично: " + msg.Message),
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
