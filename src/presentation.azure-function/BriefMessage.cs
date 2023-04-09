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

        return new BriefTelegramMessage
        {
            Id = Guid.NewGuid(),
            User = msg.User,
            UserId = msg.UserId,
            Message = CalculateTokens(msg.Message) <= MIN_TOKENS_COUNT
                ? msg.Message
                : await AskChatGptForBriefMessage(msg),
            MessageId = msg.MessageId,
            Date = msg.Date,
            DateUtc = date,
            TTL = TTL_TWO_DAYS
        };
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
                    ChatMessage.FromSystem("You are an AI that provides brief and concise answers."),
                    ChatMessage.FromUser("Make brief and short: " + msg.Message),
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
