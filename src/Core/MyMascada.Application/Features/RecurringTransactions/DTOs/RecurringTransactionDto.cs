using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.RecurringTransactions.DTOs;

/// <summary>
/// Summary DTO for a user-created recurring transaction (scheduled bill).
/// Dates (StartDate/EndDate/NextDueDate) are date-only values serialized at
/// UTC midnight; Amount is always positive (expense-only — auto-created
/// transactions are stored negative).
/// </summary>
public class RecurringTransactionDto
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public RecurrenceFrequency Frequency { get; set; }
    public int? CustomIntervalDays { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime NextDueDate { get; set; }
    public bool AutoCreate { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Detail DTO including recent occurrences
/// </summary>
public class RecurringTransactionDetailDto : RecurringTransactionDto
{
    public List<RecurringTransactionOccurrenceDto> RecentOccurrences { get; set; } = new();
}

/// <summary>
/// DTO for a processed occurrence of a recurring transaction
/// </summary>
public class RecurringTransactionOccurrenceDto
{
    public int Id { get; set; }
    public DateTime ScheduledDate { get; set; }
    public RecurringTransactionOccurrenceStatus Status { get; set; }
    public int? TransactionId { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// A materialized upcoming occurrence of a recurring transaction
/// </summary>
public class UpcomingRecurringTransactionDto
{
    public int RecurringTransactionId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public DateTime DueDate { get; set; }
    public bool AutoCreate { get; set; }
}
