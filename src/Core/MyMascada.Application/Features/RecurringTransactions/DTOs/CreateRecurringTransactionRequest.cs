using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.RecurringTransactions.DTOs;

/// <summary>
/// Request DTO for creating a recurring transaction.
/// Full validation is performed by the command validator.
/// </summary>
public class CreateRecurringTransactionRequest
{
    public int AccountId { get; set; }
    public int? CategoryId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public RecurrenceFrequency Frequency { get; set; }
    public int? CustomIntervalDays { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool AutoCreate { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Request DTO for updating a recurring transaction
/// </summary>
public class UpdateRecurringTransactionRequest
{
    public int AccountId { get; set; }
    public int? CategoryId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public RecurrenceFrequency Frequency { get; set; }
    public int? CustomIntervalDays { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool AutoCreate { get; set; }
    public string? Notes { get; set; }
}
