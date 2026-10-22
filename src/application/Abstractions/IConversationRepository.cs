using System.Threading;
using System.Threading.Tasks;
using Ungerfall.ChatGpt.TelegramBot.Database;

namespace Ungerfall.ChatGpt.TelegramBot.Abstractions;
public interface IConversationRepository
{
    Task<ConversationReceipt?> Begin(long chatId, int messageId, CancellationToken cancellation);
    Task SaveReceipt(ConversationReceipt receipt, CancellationToken cancellation);
    Task Release(ConversationReceipt receipt, CancellationToken cancellation);
    Task<ConversationState> Get(long chatId, int topicId, CancellationToken cancellation);
    Task<bool> Save(ConversationState state, CancellationToken cancellation);
    Task<ConversationBudget> GetBudget(long chatId, CancellationToken cancellation);
    Task<bool> ReserveDecision(ConversationReceipt receipt, ConversationState state, ConversationBudget budget, CancellationToken cancellation);
}
