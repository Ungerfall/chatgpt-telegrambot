using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using Ungerfall.ChatGpt.TelegramBot.Configuration;

namespace Ungerfall.ChatGpt.TelegramBot;
public class ConversationPolicy
{
    private readonly IConversationRepository _repository;
    private readonly ConversationPolicyChats _chats;
    private readonly ILogger<ConversationPolicy> _logger;
    private readonly DecisionsClient _decisions;

    public ConversationPolicy(
        IConversationRepository repository, ConversationPolicyChats chats, ILogger<ConversationPolicy> logger, DecisionsClient decisions)
    {
        _repository = repository;
        _chats = chats;
        _logger = logger;
        _decisions = decisions;
    }

    public async Task<ConversationDecision> Decide(Message message, Database.ConversationReceipt receipt, CancellationToken cancellation)
    {
        var options = _chats.Get(message.Chat.Id);
        if (!options.DecisionsEnabled || !_decisions.IsConfigured
            || (!options.ContinuationEnabled && !options.AutonomousEnabled)
            || message.Chat.Type is not (ChatType.Group or ChatType.Supergroup)
            || message.From == null || message.ReplyToMessage != null
            || message.Text?.Length > 512
            || message.Entities?.Any(x => x.Type is MessageEntityType.Mention or MessageEntityType.TextMention) == true)
            return ConversationDecision.Ignore;
        var now = DateTime.UtcNow;
        if (message.Date < now.AddMinutes(-2) || message.Date > now.AddSeconds(10)) return ConversationDecision.Ignore;
        if (receipt.DecisionAttempted)
        {
            var cached = receipt.Decision;
            if (string.IsNullOrWhiteSpace(receipt.DecisionInput) || receipt.DecisionInput.Length > 16384
                || cached == ConversationDecision.Continuation && !options.ContinuationEnabled
                || cached == ConversationDecision.Interjection && !options.AutonomousEnabled)
                return ConversationDecision.Ignore;
            if (cached == ConversationDecision.Interjection && !await TryMarkInterjection(message, cancellation))
                return ConversationDecision.Ignore;
            return cached;
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var state = await _repository.Get(message.Chat.Id, message.MessageThreadId ?? 0, cancellation);
            var continuation = options.ContinuationEnabled && !state.AutonomousPending
                && state.UserId == message.From.Id && state.ExpiresUtc > now && state.RemainingCandidates > 0;
            if (!continuation && (!options.AutonomousEnabled || state.AutonomousPending
                || !state.Context.Any(x => !x.IsBot && x.UserId != message.From.Id && x.DateUtc >= now.AddMinutes(-2))))
                return ConversationDecision.Ignore;

            var budget = await _repository.GetBudget(message.Chat.Id, cancellation);
            var today = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (budget.DateUtc != today)
            {
                budget.DateUtc = today;
                budget.Decisions = 0;
                budget.AutonomousCandidates = 0;
            }
            if (budget.Decisions >= options.DecisionDailyLimit) return ConversationDecision.Ignore;
            if (!continuation)
            {
                if (budget.AutonomousCandidates >= options.AutonomousDailyLimit
                    || budget.LastAutonomousUtc.AddMinutes(options.CooldownMinutes) > now)
                    return ConversationDecision.Ignore;
                // Stable sampling prevents retries and other instances from rerolling this message.
                var hash = SHA256.HashData(Encoding.UTF8.GetBytes(FormattableString.Invariant($"{message.Chat.Id}:{message.Id}")));
                var sample = (hash[0] * 256 + hash[1]) / 65536d;
                if (sample >= options.AutonomousProbability) return ConversationDecision.Ignore;
                budget.AutonomousCandidates++;
                budget.LastAutonomousUtc = now;
            }
            else
            {
                state.RemainingCandidates--;
            }
            budget.Decisions++;
            receipt.DecisionInput = JsonSerializer.Serialize(new
            {
                context = state.Context.Where(x => x.DateUtc >= now.AddMinutes(-2) && x.MessageId != message.Id).TakeLast(4)
                    .Select(x => new { speaker = x.IsBot ? "bot" : x.UserId == message.From.Id ? "current_user" : "other_user", text = Truncate(x.Text) }).ToArray(),
                latest = Truncate(message.Text ?? string.Empty),
            }, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            if (!await _repository.ReserveDecision(receipt, state, budget, cancellation)) continue;

            var approved = await _decisions.ShouldReply(receipt.DecisionInput, !continuation, options.MinProbability, cancellation);
            receipt.Decision = !approved ? ConversationDecision.Ignore
                : continuation ? ConversationDecision.Continuation : ConversationDecision.Interjection;
            if (approved)
            {
                await _repository.SaveReceipt(receipt, cancellation);
                if (!continuation && !await TryMarkInterjection(message, cancellation))
                {
                    receipt.Decision = ConversationDecision.Ignore;
                    await _repository.SaveReceipt(receipt, cancellation);
                }
            }
            return receipt.Decision;
        }
        _logger.LogWarning("Decision reservation conflicted in chat {ChatId}; remaining silent", message.Chat.Id);
        return ConversationDecision.Ignore;
    }

    public async Task<bool> TryMarkInterjection(Message message, CancellationToken cancellation)
    {
        if (message.Date < DateTime.UtcNow.AddMinutes(-2)) return false;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var state = await _repository.Get(message.Chat.Id, message.MessageThreadId ?? 0, cancellation);
            if (state.Context.Any(x => !x.IsBot && x.MessageId > message.Id)) return false;
            if (state.AutonomousPending) return state.AutonomousMessageId == message.Id;
            state.AutonomousPending = true;
            state.AutonomousMessageId = message.Id;
            state.RemainingCandidates = 0;
            if (await _repository.Save(state, cancellation)) return true;
        }
        _logger.LogWarning("Unable to reserve the autonomous reply in chat {ChatId}; remaining silent", message.Chat.Id);
        return false;
    }

    public async Task Observe(
        Message message, Message? reply, string? response, CancellationToken cancellation, bool autonomous = false, bool addressed = false)
    {
        var options = _chats.Get(message.Chat.Id);
        if (!options.TrackContinuation && !options.ContinuationEnabled && !options.AutonomousEnabled) return;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var state = await _repository.Get(message.Chat.Id, message.MessageThreadId ?? 0, cancellation);
            var userId = message.From?.Id ?? 0;
            if (reply != null && state.ExpiresUtc <= message.Date) state.Context.Clear();
            state.Context.RemoveAll(x => x.MessageId == message.Id);
            state.Context.Add(new Database.ConversationEntry
            {
                MessageId = message.Id,
                UserId = userId,
                Text = Truncate(message.Text ?? string.Empty),
                DateUtc = message.Date,
            });
            if (state.Context.Count > 4) state.Context.RemoveRange(0, state.Context.Count - 4);

            if (reply != null)
            {
                state.UserId = userId;
                state.BotMessageId = reply.Id;
                state.AutonomousPending = autonomous;
                state.AutonomousMessageId = autonomous ? message.Id : 0;
                state.ExpiresUtc = autonomous ? default : reply.Date.AddSeconds(options.WindowSeconds);
                state.RemainingCandidates = autonomous ? 0 : options.MaxCandidates;
                state.Context.Add(new Database.ConversationEntry
                {
                    MessageId = reply.Id,
                    UserId = reply.From?.Id ?? 0,
                    Text = Truncate(response ?? string.Empty),
                    IsBot = true,
                    DateUtc = reply.Date,
                });
                if (state.Context.Count > 4) state.Context.RemoveAt(0);
            }
            else if (state.UserId != userId || message.ReplyToMessage != null || message.Text?.StartsWith('/') == true)
            {
                state.RemainingCandidates = 0;
            }
            if (addressed)
            {
                state.AutonomousPending = false;
                state.AutonomousMessageId = 0;
            }

            if (await _repository.Save(state, cancellation)) return;
        }
        throw new InvalidOperationException("Conversation state changed concurrently; retry later.");
    }

    private static string Truncate(string text)
    {
        var length = Math.Min(512, text.Length);
        if (length < text.Length && char.IsHighSurrogate(text[length - 1]) && char.IsLowSurrogate(text[length])) length--;
        return text[..length];
    }
}

public enum ConversationDecision
{
    Ignore,
    Continuation,
    Interjection,
}
