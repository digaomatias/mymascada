using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.RecurringTransactions.Commands;

/// <summary>
/// Shared shape for create/update recurring transaction commands so
/// validation rules can be defined once.
/// </summary>
public interface IRecurringTransactionBaseCommand
{
    Guid UserId { get; }
    int AccountId { get; }
    int? CategoryId { get; }
    string Description { get; }
    decimal Amount { get; }
    RecurrenceFrequency Frequency { get; }
    int? CustomIntervalDays { get; }
    DateTime StartDate { get; }
    DateTime? EndDate { get; }
    bool AutoCreate { get; }
    string? Notes { get; }
}
