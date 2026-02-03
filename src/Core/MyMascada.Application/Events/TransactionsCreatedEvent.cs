using MediatR;

namespace MyMascada.Application.Events;

/// <summary>
/// Event published when new transactions are created, triggering asynchronous categorization processing
/// </summary>
public class TransactionsCreatedEvent : INotification
{
    public List<int> TransactionIds { get; }
    public Guid UserId { get; }
    public DateTime CreatedAt { get; }

    public TransactionsCreatedEvent(List<int> transactionIds, Guid userId)
    {
        TransactionIds = transactionIds ?? throw new ArgumentNullException(nameof(transactionIds));
        UserId = userId;
        CreatedAt = DateTime.UtcNow;
    }
}