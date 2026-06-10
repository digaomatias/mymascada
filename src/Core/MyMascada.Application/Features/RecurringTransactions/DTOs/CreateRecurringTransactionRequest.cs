using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.RecurringTransactions.DTOs;

/// <summary>
/// Request DTO for creating a recurring transaction.
/// Full validation is performed by the command validator.
///
/// API notes:
/// - StartDate/EndDate are treated as date-only values (the time component is
///   discarded server-side). Clients should send date-only strings like
///   "2026-07-31" — sending timezone-offset ISO strings can shift the value
///   across midnight and change the anchor day used for monthly clamping.
/// - Amount is a positive value. Recurring transactions are expense-only:
///   auto-created transactions are stored with a negative amount. Recurring
///   income would require a future direction flag.
/// </summary>
public class CreateRecurringTransactionRequest
{
    public int AccountId { get; set; }
    public int? CategoryId { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Positive bill amount (expense-only; stored negative when auto-created)
    /// </summary>
    public decimal Amount { get; set; }

    public RecurrenceFrequency Frequency { get; set; }
    public int? CustomIntervalDays { get; set; }

    /// <summary>
    /// Date-only. Anchors the day-of-month for Monthly/Yearly clamping.
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Date-only. Last date on which an occurrence may fall.
    /// </summary>
    public DateTime? EndDate { get; set; }

    public bool AutoCreate { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Request DTO for updating a recurring transaction.
/// Same API notes as <see cref="CreateRecurringTransactionRequest"/>:
/// dates are date-only, amount is positive (expense-only).
/// </summary>
public class UpdateRecurringTransactionRequest
{
    public int AccountId { get; set; }
    public int? CategoryId { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Positive bill amount (expense-only; stored negative when auto-created)
    /// </summary>
    public decimal Amount { get; set; }

    public RecurrenceFrequency Frequency { get; set; }
    public int? CustomIntervalDays { get; set; }

    /// <summary>
    /// Date-only. Anchors the day-of-month for Monthly/Yearly clamping.
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Date-only. Last date on which an occurrence may fall.
    /// </summary>
    public DateTime? EndDate { get; set; }

    public bool AutoCreate { get; set; }
    public string? Notes { get; set; }
}
