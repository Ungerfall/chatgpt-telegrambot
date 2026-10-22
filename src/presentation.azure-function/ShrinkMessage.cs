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
        var minimumTokens = int.Parse(Environment.GetEnvironmentVariable("ShrinkMinimumTokens", EnvironmentVariableTarget.Process) ?? "512");
        if (minimumTokens is < 1 or > 32768) throw new ArgumentOutOfRangeException(nameof(minimumTokens));
        await foreach (var msg in _messagesRepository.GetOldMessages(minimumTokens, CancellationToken.None))
        {
            var tokensBefore = _tokenCounter.Count(msg.Message);
            if (!await _messagesRepository.TryBeginShrink(msg, tokensBefore, minimumTokens, CancellationToken.None)) continue;
            try
            {
                var shrunkMsg = await AskChatGptToShrink(msg);
                var tokensAfter = _tokenCounter.Count(shrunkMsg);
                if (tokensAfter >= tokensBefore)
                {
                    await _messagesRepository.CompleteShrink(msg, processed: true, CancellationToken.None);
                }
                else
                {
                    await _messagesRepository.Update(new TelegramMessage
                    {
                        Id = msg.Id,
                        ChatId = msg.ChatId,
                        User = msg.User,
                        UserId = msg.UserId,
                        Message = shrunkMsg,
                        MessageId = msg.MessageId,
                        MessageThreadId = msg.MessageThreadId,
                        Date = msg.Date,
                        DateUtc = msg.DateUtc,
                        TTL = msg.TTL,
                        IsShrunk = true,
                        ShrinkProcessed = true,
                        ETag = msg.ETag,
                        OriginalMessage = msg.OriginalMessage ?? msg.Message,
                    }, CancellationToken.None);
                }
            }
            catch
            {
                try
                {
                    await _messagesRepository.CompleteShrink(msg, processed: false, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to release shrink lease for ({ChatId}, {MessageId})", msg.ChatId, msg.MessageId);
                }
                throw;
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
        _logger.LogInformation("Shrink tokens for chat {ChatId}: {@tokens}", msg.ChatId,
            new { completionResult.Usage?.PromptTokens, completionResult.Usage?.CompletionTokens, completionResult.Usage?.TotalTokens });
        var content = completionResult.Choices?.Count > 0 ? completionResult.Choices[0].Message?.Content : null;
        return completionResult.Successful && !string.IsNullOrWhiteSpace(content)
            ? content
            : throw new InvalidOperationException("ChatGPT shrink request wasn't successful or returned no content");
    }
}
