using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OpenAI.Interfaces;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using System;
using System.Threading;
using System.Threading.Tasks;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using Ungerfall.ChatGpt.TelegramBot.Database;

namespace Ungerfall.ChatGpt.TelegramBot.AzureFunction;

/// <summary>
/// Shrinks old messages using Chat GPT
/// </summary>
public class ShrinkMessage
{
    private const int MIN_MESSAGE_LENGTH = 25;

    private readonly ILogger _logger;
    private readonly IOpenAIService _openAiService;
    private readonly ITelegramMessageRepository _messagesRepository;
    private readonly ITokenCounter _tokenCounter;

    public ShrinkMessage(
        IOpenAIService openAiService,
        ITokenCounter tokenCounter,
        ILogger<ShrinkMessage> logger,
        ITelegramMessageRepository messagesRepository)
    {
        _openAiService = openAiService;
        _tokenCounter = tokenCounter;
        _logger = logger;
        _messagesRepository = messagesRepository;
    }

    [Function("ShrinkMessage")]
    public async Task Run([TimerTrigger("0 */30 * * * *")] TimerInfo timer)
    {
        _logger.LogInformation("C# Timer trigger function executed at: {now}", DateTime.Now);
        await foreach (var msg in _messagesRepository.GetOldMessages(MIN_MESSAGE_LENGTH, CancellationToken.None))
        {
            var tokensBefore = _tokenCounter.Count(msg.Message);
            var shrunkMsg = await AskChatGptToShrink(msg);
            var tokensAfter = _tokenCounter.Count(shrunkMsg);
            // update if only chat GPT managed to shrunk a message
            if (tokensAfter < tokensBefore)
            {
                await _messagesRepository.Update(new TelegramMessage
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

        _logger.LogInformation("Next timer schedule at: {nextSchedule}", timer?.ScheduleStatus?.Next);
    }

    private async Task<string> AskChatGptToShrink(TelegramMessage msg)
    {
        var completionResult = await _openAiService.ChatCompletion.Create(
            new ChatCompletionCreateRequest
            {
                Messages =
                [
                    ChatMessage.FromSystem("Assistant is an intelligent chatbot designed to shrink chat messages, so that the messages will be used later to feed chat GPT api."
                            + "Instructions:"
                            + "- Chat language is Russian."
                            + "- Keep context as close as possible."
                            + "- Do not remove mentions starting with @."
                            + "- Do not remove links."),
                    ChatMessage.FromUser("Shrink the message: " + msg.Message),
                ],
                Temperature = 0f,
                User = msg.User,
                Model = Models.Gpt_4o_mini,
            },
            Models.Model.Gpt_4o_mini);
        return completionResult.Successful
            ? completionResult?.Choices[0]?.Message?.Content ?? "Successful, but no content."
            : throw new InvalidOperationException("ChatGPT request wasn't successful");
    }
}
