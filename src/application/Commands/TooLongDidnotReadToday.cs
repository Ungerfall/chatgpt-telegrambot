using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using OpenAI.Interfaces;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using static System.Environment;

namespace Ungerfall.ChatGpt.TelegramBot.Commands;
public class TooLongDidNotReadToday
{
    private const string AskForTLDR = "Напиши TL;DR переписки маркированным списком.";
    private readonly ITelegramMessageRepository _history;
    private readonly ITokenCounter _tokenCounter;
    private readonly ITelegramBotClient _botClient;
    private readonly IOpenAIService _openAiService;
    private readonly ILogger<TooLongDidNotReadToday> _logger;
    private readonly IWhitelist _whitelist;
    private readonly ITimedTaskExecutionRepository _cache;

    public TooLongDidNotReadToday(
        ITelegramMessageRepository history,
        ITokenCounter tokenCounter,
        IOpenAIService openAiService,
        ILogger<TooLongDidNotReadToday> logger,
        ITelegramBotClient botClient,
        IWhitelist whitelist,
        ITimedTaskExecutionRepository cache)
    {
        _history = history;
        _tokenCounter = tokenCounter;
        _openAiService = openAiService;
        _logger = logger;
        _botClient = botClient;
        _whitelist = whitelist;
        _cache = cache;
    }

    public async Task<Message> Execute(
        long chatId,
        CancellationToken cancellation = default,
        bool useRich = false,
        int? messageThreadId = default,
        Func<Task>? beforeSend = default)
    {
        await _botClient.SendChatAction(
            chatId: chatId,
            ChatAction.Typing,
            cancellationToken: cancellation);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        using var historyHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var requests = new List<ChatCompletionCreateRequest>();
        await foreach (var batch in ChatMessageBuilder.CreateBatches(
            GetHistory(chatId, today, messageThreadId, historyHash, cancellation), _whitelist.GetSystemRoleMessage(chatId), AskForTLDR, _tokenCounter, cancellation))
        {
            requests.Add(new ChatCompletionCreateRequest { Messages = batch, Temperature = .2f, Model = Models.Gpt_4o_mini });
        }

        string? summary = null;
        if (requests.Count > 0)
        {
            // Hash the exact prepared messages, including role, instruction and batch boundaries.
            var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            {
                version = 1, chatId, day = today, topic = messageThreadId ?? 0,
                history = Convert.ToHexString(historyHash.GetHashAndReset()),
                requests = requests.Select(r => new { r.Model, r.Temperature, messages = r.Messages.Select(m => new { m.Role, m.Content }) }),
            })));
            var cache = await _cache.BeginTldr(chatId, FormattableString.Invariant($"tldr:{today:yyyy-MM-dd}:{messageThreadId ?? 0}:{hash}"), cancellation);
            summary = cache.Summary;
            _logger.LogInformation("TLDR cache hit: {Hit}, chat {ChatId}, topic {TopicId}", summary != null, chatId, messageThreadId ?? 0);
            if (summary == null)
            {
                try
                {
                    var summaries = await Task.WhenAll(requests.Select(r => SendGptRequest(r, chatId, cancellation)));
                    summary = string.Join(NewLine, summaries.Select(x => x.Text));
                    if (summaries.All(x => x.Successful))
                    {
                        cache.Summary = summary;
                        try { await _cache.CompleteTldr(cache, cancellation); }
                        catch (CosmosException ex)
                        {
                            _logger.LogError(ex, "Unable to cache TLDR for chat {ChatId}; sending the generated summary", chatId);
                            await ReleaseCache(cache);
                        }
                    }
                    else await ReleaseCache(cache);
                }
                catch
                {
                    try { await ReleaseCache(cache); }
                    catch (Exception ex) { _logger.LogError(ex, "Unable to release TLDR generation for chat {ChatId}", chatId); }
                    throw;
                }
            }
        }
        var telegramMessage = summary == null
            ? "Сегодня ничего не произошло"
            : $"{summary}{NewLine}TL;DR не записывается в историю";
        if (beforeSend != null) await beforeSend();
        return await _botClient.SendResponse(
            chatId: chatId,
            text: telegramMessage,
            useRich: useRich,
            messageThreadId: messageThreadId,
            cancellationToken: cancellation);
    }

    private async Task ReleaseCache(Database.TldrCache cache)
    {
        try { await _cache.ReleaseTldr(cache, CancellationToken.None); }
        catch (CosmosException ex) { _logger.LogError(ex, "Unable to release TLDR generation for chat {ChatId}", cache.ChatId); }
    }

    private async IAsyncEnumerable<Database.TelegramMessage> GetHistory(
        long chatId, DateOnly today, int? messageThreadId, IncrementalHash fingerprint,
        [EnumeratorCancellation] CancellationToken cancellation = default)
    {
        var messages = new List<Database.TelegramMessage>();
        await foreach (var message in _history.Get(chatId, today, cancellation))
            if ((message.MessageThreadId ?? 0) == (messageThreadId ?? 0)) messages.Add(message);
        foreach (var message in messages.OrderByDescending(x => x.Date).ThenByDescending(x => x.MessageId).ThenBy(x => x.Id, StringComparer.Ordinal))
        {
            fingerprint.AppendData(JsonSerializer.SerializeToUtf8Bytes(new
            {
                message.Id, message.MessageId, message.MessageThreadId, message.User, message.UserId, message.Message, message.Date,
            }));
            yield return message;
        }
    }

    private async Task<(string Text, bool Successful)> SendGptRequest(ChatCompletionCreateRequest request, long chatId, CancellationToken cancellation = default)
    {
        var completionResult = await _openAiService.ChatCompletion.Create(
            request,
            Models.Model.Gpt_4o_mini,
            cancellationToken: cancellation);
        _logger.LogInformation("TLDR tokens for chat {ChatId}: {@tokens}", chatId,
            new
            {
                completionResult.Usage?.CompletionTokens,
                completionResult.Usage?.PromptTokens,
                completionResult.Usage?.TotalTokens
            });
        var content = completionResult.Choices?.Count > 0 ? completionResult.Choices[0].Message?.Content : null;
        if (completionResult.Successful && !string.IsNullOrWhiteSpace(content))
        {
            return (content, true);
        }
        else
        {
            _logger.LogError("ChatGPT error: {@error}", new { completionResult.Error?.Type, completionResult.Error?.Message });
            return ("ChatGPT request wasn't successful", false);
        }
    }
}
