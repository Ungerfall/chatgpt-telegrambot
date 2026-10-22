using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Ungerfall.ChatGpt.TelegramBot.Abstractions;

namespace Ungerfall.ChatGpt.TelegramBot.Database;
public class ConversationRepository : IConversationRepository
{
    private readonly Container _container;
    private readonly ILogger<ConversationRepository> _logger;

    public ConversationRepository(CosmosClient cosmos, IOptions<CosmosDbOptions> options, ILogger<ConversationRepository> logger)
    {
        _container = cosmos.GetContainer(options.Value.DatabaseId, options.Value.TimedTasksContainerId);
        _logger = logger;
    }

    public async Task<ConversationReceipt?> Begin(long chatId, int messageId, CancellationToken cancellation)
    {
        var id = $"conversation-message:{messageId}";
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var receipt = await Read<ConversationReceipt>(id, chatId, cancellation);
            if (receipt != null && receipt.Stage != ConversationReceipt.Processing)
            {
                if (receipt.Stage == ConversationReceipt.Sending)
                {
                    _logger.LogError("Delivery of ({ChatId}, {MessageId}) was started but not confirmed; skipping resend", chatId, messageId);
                }
                return null;
            }
            if (receipt != null && receipt.LeaseUntilUtc > DateTime.UtcNow)
            {
                throw new InvalidOperationException($"Message ({chatId}, {messageId}) is already being processed; retry later.");
            }

            receipt ??= new ConversationReceipt { Id = id, ChatId = chatId };
            receipt.LeaseId = Guid.NewGuid().ToString();
            receipt.LeaseUntilUtc = DateTime.UtcNow.AddMinutes(5);
            try
            {
                await SaveReceipt(receipt, cancellation);
                return receipt;
            }
            catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed)
            {
                // Another instance acquired or renewed this message lease.
            }
        }
        throw new InvalidOperationException($"Unable to acquire message ({chatId}, {messageId}); retry later.");
    }

    public async Task SaveReceipt(ConversationReceipt receipt, CancellationToken cancellation)
    {
        ItemResponse<ConversationReceipt> response = receipt.ETag == null
            ? await _container.CreateItemAsync(receipt, new PartitionKey(receipt.ChatId), cancellationToken: cancellation)
            : await _container.ReplaceItemAsync(receipt, receipt.Id, new PartitionKey(receipt.ChatId),
                new ItemRequestOptions { IfMatchEtag = receipt.ETag }, cancellation);
        receipt.ETag = response.ETag;
    }

    public async Task Release(ConversationReceipt receipt, CancellationToken cancellation)
    {
        var current = await Read<ConversationReceipt>(receipt.Id, receipt.ChatId, cancellation);
        if (current?.LeaseId != receipt.LeaseId) return;
        if (current.DecisionAttempted)
        {
            current.LeaseUntilUtc = DateTime.UtcNow;
            await SaveReceipt(current, cancellation);
            return;
        }
        await _container.DeleteItemAsync<ConversationReceipt>(receipt.Id, new PartitionKey(receipt.ChatId),
            new ItemRequestOptions { IfMatchEtag = current.ETag }, cancellation);
    }

    public async Task<ConversationState> Get(long chatId, int topicId, CancellationToken cancellation)
    {
        var id = $"conversation-topic:{topicId}";
        return await Read<ConversationState>(id, chatId, cancellation)
            ?? new ConversationState { Id = id, ChatId = chatId, TopicId = topicId };
    }

    public async Task<bool> Save(ConversationState state, CancellationToken cancellation)
    {
        try
        {
            if (state.ETag == null)
            {
                await _container.CreateItemAsync(state, new PartitionKey(state.ChatId), cancellationToken: cancellation);
            }
            else
            {
                await _container.ReplaceItemAsync(state, state.Id, new PartitionKey(state.ChatId),
                    new ItemRequestOptions { IfMatchEtag = state.ETag }, cancellation);
            }
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed)
        {
            return false;
        }
    }

    public async Task<ConversationBudget> GetBudget(long chatId, CancellationToken cancellation)
    {
        return await Read<ConversationBudget>("conversation-budget", chatId, cancellation)
            ?? new ConversationBudget { ChatId = chatId };
    }

    public async Task<bool> ReserveDecision(
        ConversationReceipt receipt, ConversationState state, ConversationBudget budget, CancellationToken cancellation)
    {
        var batch = _container.CreateTransactionalBatch(new PartitionKey(receipt.ChatId));
        if (state.ETag == null) batch.CreateItem(state);
        else batch.ReplaceItem(state.Id, state, new TransactionalBatchItemRequestOptions { IfMatchEtag = state.ETag });
        if (budget.ETag == null) batch.CreateItem(budget);
        else batch.ReplaceItem(budget.Id, budget, new TransactionalBatchItemRequestOptions { IfMatchEtag = budget.ETag });
        receipt.DecisionAttempted = true;
        batch.ReplaceItem(receipt.Id, receipt, new TransactionalBatchItemRequestOptions { IfMatchEtag = receipt.ETag });
        using var result = await batch.ExecuteAsync(cancellation);
        if (result.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed)
        {
            receipt.DecisionAttempted = false;
            return false;
        }
        if (!result.IsSuccessStatusCode)
        {
            throw new CosmosException(result.ErrorMessage, result.StatusCode, 0, result.ActivityId, result.RequestCharge);
        }
        receipt.ETag = result.GetOperationResultAtIndex<ConversationReceipt>(2).ETag;
        return true;
    }

    private async Task<T?> Read<T>(string id, long chatId, CancellationToken cancellation) where T : class
    {
        try
        {
            var response = await _container.ReadItemAsync<T>(id, new PartitionKey(chatId), cancellationToken: cancellation);
            if (response.Resource is ConversationReceipt receipt) receipt.ETag = response.ETag;
            if (response.Resource is ConversationState state) state.ETag = response.ETag;
            if (response.Resource is ConversationBudget budget) budget.ETag = response.ETag;
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
