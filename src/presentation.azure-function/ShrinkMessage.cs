using Azure;
using Azure.AI.OpenAI;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System;
using System.ClientModel;
using System.Threading;
using System.Threading.Tasks;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using Ungerfall.ChatGpt.TelegramBot.AzureOpenAi;
using Ungerfall.ChatGpt.TelegramBot.Database;

namespace Ungerfall.ChatGpt.TelegramBot.AzureFunction;

/// <summary>
/// Shrinks old messages using Chat GPT
/// </summary>
public class ShrinkMessage(
    ITokenCounter tokenCounter,
    ILogger<ShrinkMessage> logger,
    ITelegramMessageRepository messagesRepository,
    IConfiguration configuration)
{
    private const int MIN_MESSAGE_LENGTH = 25;

    private readonly ILogger _logger = logger;
    private readonly Uri _azureOpenAi41NanoUri = new(configuration["AZURE_OPENAI_4_1_NANO_URI"] ?? throw new ArgumentNullException(nameof(configuration)));
    private readonly string _azureOpenAi41NanoApiKey = new(configuration["AZURE_OPENAI_4_1_NANO_API_KEY"] ?? throw new ArgumentNullException(nameof(configuration)));

    [Function("ShrinkMessage")]
    public async Task Run([TimerTrigger("0 */30 * * * *")] TimerInfo timer)
    {
        AzureOpenAIClient azureClient = new(_azureOpenAi41NanoUri, new AzureKeyCredential(_azureOpenAi41NanoApiKey));
        ChatClient chat = azureClient.GetChatClient(Deployments.Gpt_4_1_nano);
        _logger.LogInformation("C# Timer trigger function executed at: {Now}", DateTime.Now);
        await foreach (var msg in messagesRepository.GetOldMessages(MIN_MESSAGE_LENGTH, CancellationToken.None))
        {
            var tokensBefore = tokenCounter.Count(msg.Message);
            var shrunkMsg = await AskChatGptToShrink(chat, msg);
            var tokensAfter = tokenCounter.Count(shrunkMsg);
            // update if only chat GPT managed to shrunk a message
            if (tokensAfter < tokensBefore)
            {
                await messagesRepository.Update(new TelegramMessage
                {
                    Id = msg.Id,
                    ChatId = msg.ChatId,
                    User = msg.User,
                    UserId = msg.UserId,
                    Message = shrunkMsg,
                    MessageId = msg.MessageId,
                    Date = msg.Date,
                    DateUtc = msg.DateUtc,
                    TTL = msg.TTL,
                    IsShrunk = true,
                    OriginalMessage = msg.Message,
                }, CancellationToken.None);
            }
        }

        _logger.LogInformation("Next timer schedule at: {NextSchedule}", timer?.ScheduleStatus?.Next);
    }

    private async Task<string> AskChatGptToShrink(ChatClient chat, TelegramMessage msg)
    {
        ClientResult<ChatCompletion> completionResult = await chat.CompleteChatAsync(
            [
                ChatMessage.CreateSystemMessage("Assistant is an intelligent chatbot designed to shrink chat messages, so that the messages will be used later to feed chat GPT api."
                            + "Instructions:"
                            + "- Chat language is Russian."
                            + "- Keep context as close as possible."
                            + "- Do not remove mentions starting with @."
                            + "- Do not remove links."),
                ChatMessage.CreateUserMessage("Shrink the message: " + msg.Message),
            ],
            new ChatCompletionOptions
            {
                Temperature = 0f,
                EndUserId = msg.User,
            });

        if (completionResult.GetRawResponse().IsError)
        {
            throw new InvalidOperationException("ChatGPT request wasn't successful");
        }
        else
        {
            return completionResult.Value.Content.Count == 0
                ? "Successful, but no content."
                : string.Join(';', completionResult.Value.Content);
        }
    }
}
