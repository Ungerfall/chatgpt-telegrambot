using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;
using Ungerfall.ChatGpt.TelegramBot.Database;

namespace Ungerfall.ChatGpt.TelegramBot;
public sealed class ChatMessageBuilder :
    // fluent interfaces
    IChatMessageBuilderTokenCounterState,
    IChatMessageBuilderSystemRoleState,
    IChatMessageBuilderAddMessagesState
{
    private const string BotUser = "chatgpt_ungerfall_bot";
    private readonly int _maxTokens;

    private ITokenCounter _tokenCounter = null!;
    private int _tokensSum;
    private List<ChatMessage> _message = [];
    private ChatMessage? _lastUserMessage;

    private ChatMessageBuilder(int maxTokens)
    {
        _maxTokens = maxTokens;
    }

    public static IChatMessageBuilderTokenCounterState Create(int maxTokens = 1638)
    {
        return new ChatMessageBuilder(maxTokens);
    }

    public IChatMessageBuilderSystemRoleState WithTokenCounter(ITokenCounter? counter = null)
    {
        _tokenCounter = counter ?? new TokenCounter();
        return this;
    }

    public IChatMessageBuilderAddMessagesState WithSystemRoleMessage(string msg, string? lastUserMessage = null)
    {
        _message = [ChatMessage.FromSystem(msg)];
        _lastUserMessage = lastUserMessage == null ? null : ChatMessage.FromUser(lastUserMessage);
        // Chat framing follows the gpt-4o/mini estimate; API usage remains the authoritative count.
        _tokensSum = 3 + Count(_message[0]) + (_lastUserMessage == null ? 0 : Count(_lastUserMessage));
        return this;
    }

    public IChatMessageBuilderAddMessagesState AddMessage(TelegramMessage message, int? index = null)
    {
        Add(Format(message, message.Message), index);
        return this;
    }

    private void Add(ChatMessage message, int? index = null)
    {
        if (!CanAdd(message)) throw new InvalidOperationException("Message exceeds the prepared input token budget.");
        if (index == null) _message.Add(message);
        else _message.Insert(index.Value, message);
        _tokensSum += Count(message);
    }

    public ChatMessage[] Build()
    {
        return _lastUserMessage == null ? [.. _message] : [.. _message, _lastUserMessage];
    }

    public bool CanAddMessage(TelegramMessage message) => CanAdd(Format(message, message.Message));

    private bool CanAdd(ChatMessage message) => _tokensSum + Count(message) <= _maxTokens;

    private int Count(ChatMessage message) => 3 + _tokenCounter.Count(message.Role) + _tokenCounter.Count(message.Content ?? string.Empty);

    private static ChatMessage Format(TelegramMessage message, string text) => message.User == BotUser
        ? ChatMessage.FromAssistant(text) : ChatMessage.FromUser($"{message.User}: {text}");

    public int TokensCount => _tokensSum;

    public bool ContainsHistory => _message.Count > 1;

    public static async IAsyncEnumerable<ChatMessage[]> CreateBatches(
        IAsyncEnumerable<TelegramMessage> history, string systemRole, string instruction,
        ITokenCounter? tokenCounter = null, [EnumeratorCancellation] CancellationToken cancellation = default)
    {
        var budget = int.Parse(Environment.GetEnvironmentVariable("SummaryChunkTokens", EnvironmentVariableTarget.Process) ?? "3072");
        if (budget is < 512 or > 32768) throw new ArgumentOutOfRangeException(nameof(budget));
        ChatMessageBuilder NewBuilder()
        {
            var result = new ChatMessageBuilder(budget);
            result.WithTokenCounter(tokenCounter);
            result.WithSystemRoleMessage(systemRole, instruction);
            return result;
        }

        var builder = NewBuilder();
        await foreach (var message in history.WithCancellation(cancellation))
        {
            if (builder.CanAddMessage(message))
            {
                builder.AddMessage(message, 1);
                continue;
            }
            if (builder.ContainsHistory)
            {
                yield return builder.Build();
                builder = NewBuilder();
            }
            if (builder.CanAddMessage(message))
            {
                builder.AddMessage(message, 1);
                continue;
            }

            // Split an oversized message without dropping characters or cutting a surrogate pair.
            for (var offset = 0; offset < message.Message.Length;)
            {
                cancellation.ThrowIfCancellationRequested();
                var low = 1;
                var high = message.Message.Length - offset;
                var length = 0;
                while (low <= high)
                {
                    var middle = low + (high - low) / 2;
                    var candidate = middle;
                    if (offset + candidate < message.Message.Length && char.IsHighSurrogate(message.Message[offset + candidate - 1])
                        && char.IsLowSurrogate(message.Message[offset + candidate])) candidate--;
                    if (candidate == 0) { low = middle + 1; continue; }
                    if (builder.CanAdd(Format(message, message.Message.Substring(offset, candidate))))
                    {
                        length = candidate;
                        low = middle + 1;
                    }
                    else high = middle - 1;
                }
                if (length == 0) throw new InvalidOperationException("Summary token budget leaves no room for message content.");
                builder.Add(Format(message, message.Message.Substring(offset, length)));
                yield return builder.Build();
                builder = NewBuilder();
                offset += length;
            }
        }
        if (builder.ContainsHistory) yield return builder.Build();
    }
}
