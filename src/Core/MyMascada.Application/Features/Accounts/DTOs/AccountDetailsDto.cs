using System;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Accounts.DTOs;

/// <summary>
/// Detailed account information including monthly spending data for account detail views
/// </summary>
public class AccountDetailsDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public AccountType Type { get; set; }
    public string TypeDisplayName { get; set; } = string.Empty;
    public string? Institution { get; set; }
    public string? LastFourDigits { get; set; }
    public decimal CurrentBalance { get; set; }
    public decimal CalculatedBalance { get; set; }
    public string Currency { get; set; } = "USD";
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Reconciliation status
    public DateTime? LastReconciledDate { get; set; }
    public decimal? LastReconciledBalance { get; set; }

    // Monthly spending data
    public MonthlySpendingDto MonthlySpending { get; set; } = new();
}

/// <summary>
/// Monthly spending information for an account
/// </summary>
public class MonthlySpendingDto
{
    public decimal CurrentMonthSpending { get; set; }
    public decimal PreviousMonthSpending { get; set; }
    public decimal ChangeAmount { get; set; }
    public decimal ChangePercentage { get; set; }
    public string TrendDirection { get; set; } = "neutral"; // "up", "down", "neutral"
    public string MonthName { get; set; } = string.Empty;
    public int Year { get; set; }
}