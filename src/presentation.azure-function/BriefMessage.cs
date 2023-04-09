using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OpenAI.GPT3.Interfaces;
using OpenAI.GPT3.ObjectModels.RequestModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using Ungerfall.ChatGpt.TelegramBot.Database;
using Ungerfall.ChatGpt.TelegramBot.Queue;

namespace azure_function;

public class BriefMessage
{
    private const int MIN_TOKENS_COUNT = 5;
    private const int TTL_TWO_DAYS = 2 * 24 * 60 * 60;

    private readonly ILogger _logger;
    private readonly IOpenAIService _openAiService;

    public BriefMessage(ILogger<BriefMessage> logger, IOpenAIService openAiService)
    {
        _logger = logger;
        _openAiService = openAiService;
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
        var tokensCount = CalculateTokens(msg.Message);
        var briefMsg = new BriefTelegramMessage
        {
            Id = Guid.NewGuid(),
            User = msg.User,
            UserId = msg.UserId,
            MessageId = msg.MessageId,
            Date = msg.Date,
            DateUtc = date,
            TTL = TTL_TWO_DAYS
        };

        if (tokensCount <= MIN_TOKENS_COUNT)
        {
            briefMsg.Message = msg.Message;
        }
        else
        {
            var gpt = await AskChatGptForBriefMessage(msg);
            var gptTokensCount = CalculateTokens(gpt);
            // if gpt version is longer keep original version
            briefMsg.Message = gptTokensCount >= tokensCount
                ? msg.Message
                : briefMsg.Message = gpt;
        }

        return briefMsg;
    }

    private static int CalculateTokens(string msg)
    {
        // OpenAI.GPT3.Tokenizer.GPT3.TokenizerGpt3.TokenCount couldn't count Cyrillic at the moment.
        return msg.Count(char.IsWhiteSpace) + 1;
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
