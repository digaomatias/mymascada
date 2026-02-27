namespace MyMascada.Application.Features.Transactions.Commands;

/// <summary>
/// Shared properties between CreateTransactionCommand and UpdateTransactionCommand,
/// used to constrain the shared validator base class.
/// </summary>
public interface ITransactionBaseCommand
{
    decimal Amount { get; }
    string Description { get; }
    DateTime TransactionDate { get; }
    string? UserDescription { get; }
    string? Notes { get; }
    string? Location { get; }
    string? Tags { get; }
}
