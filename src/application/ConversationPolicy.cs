using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using Ungerfall.ChatGpt.TelegramBot.Configuration;

namespace Ungerfall.ChatGpt.TelegramBot;
public class ConversationPolicy
{
    private readonly IConversationRepository _repository;
    private readonly ConversationPolicyChats _chats;
    private readonly ILogger<ConversationPolicy> _logger;

    public ConversationPolicy(IConversationRepository repository, ConversationPolicyChats chats, ILogger<ConversationPolicy> logger)
    {
        _repository = repository;
        _chats = chats;
        _logger = logger;
    }

    public async Task Observe(Message message, Message? reply, string? response, CancellationToken cancellation)
    {
        var options = _chats.Get(message.Chat.Id);
        if (!options.TrackContinuation) return;

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
            });
            if (state.Context.Count > 4) state.Context.RemoveRange(0, state.Context.Count - 4);

            if (reply != null)
            {
                state.UserId = userId;
                state.BotMessageId = reply.Id;
                state.ExpiresUtc = reply.Date.AddSeconds(options.WindowSeconds);
                state.RemainingCandidates = options.MaxCandidates;
                state.Context.Add(new Database.ConversationEntry
                {
                    MessageId = reply.Id,
                    UserId = reply.From?.Id ?? 0,
                    Text = Truncate(response ?? string.Empty),
                });
                if (state.Context.Count > 4) state.Context.RemoveAt(0);
            }
            else if (state.UserId != userId || message.ReplyToMessage != null || message.Text?.StartsWith('/') == true)
            {
                state.RemainingCandidates = 0;
            }
            else if (userId != 0 && state.ExpiresUtc > DateTime.UtcNow && state.RemainingCandidates > 0)
            {
                state.RemainingCandidates--;
                _logger.LogDebug("Continuation candidate in ({ChatId}, {TopicId}); classifier is not connected, remaining silent",
                    message.Chat.Id, state.TopicId);
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
