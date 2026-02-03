using System.Collections.Generic;

namespace MyMascada.Application.Features.Transactions.DTOs;

/// <summary>
/// Response DTO for paginated transaction lists
/// </summary>
public class TransactionListResponse
{
    /// <summary>
    /// List of transactions for the current page
    /// </summary>
    public List<TransactionDto> Transactions { get; set; } = new();

    /// <summary>
    /// Summary metadata for all filtered transactions (not just current page)
    /// </summary>
    public TransactionSummaryDto Summary { get; set; } = new();

    /// <summary>
    /// Total number of transactions across all pages
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Current page number (1-based)
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Total number of pages
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// Whether there are more pages after this one
    /// </summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Whether there are pages before this one
    /// </summary>
    public bool HasPreviousPage => Page > 1;
}

/// <summary>
/// Summary metadata for filtered transactions
/// </summary>
public class TransactionSummaryDto
{
    /// <summary>
    /// Total balance of all filtered transactions (sum of all amounts)
    /// </summary>
    public decimal TotalBalance { get; set; }

    /// <summary>
    /// Total income amount (sum of positive amounts)
    /// </summary>
    public decimal TotalIncome { get; set; }

    /// <summary>
    /// Total expense amount (sum of negative amounts, shown as positive)
    /// </summary>
    public decimal TotalExpenses { get; set; }

    /// <summary>
    /// Count of transactions with positive amounts (income)
    /// </summary>
    public int IncomeTransactionCount { get; set; }

    /// <summary>
    /// Count of transactions with negative amounts (expenses)
    /// </summary>
    public int ExpenseTransactionCount { get; set; }

    /// <summary>
    /// Count of transactions that are transfers
    /// </summary>
    public int TransferTransactionCount { get; set; }

    /// <summary>
    /// Count of unreviewed transactions
    /// </summary>
    public int UnreviewedTransactionCount { get; set; }
}