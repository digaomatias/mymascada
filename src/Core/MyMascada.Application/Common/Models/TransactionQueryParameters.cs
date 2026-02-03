using MyMascada.Domain.Enums;

namespace MyMascada.Application.Common.Models;

/// <summary>
/// Common parameters for querying transactions across different contexts
/// </summary>
public class TransactionQueryParameters
{
    public Guid UserId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int? AccountId { get; set; }
    public int? CategoryId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public TransactionStatus? Status { get; set; }
    public string? SearchTerm { get; set; }
    public bool? IsReviewed { get; set; }
    public bool? IsReconciled { get; set; }
    public bool? IsExcluded { get; set; }
    public bool? NeedsCategorization { get; set; }
    public bool? IncludeTransfers { get; set; }
    public bool? OnlyTransfers { get; set; }
    public Guid? TransferId { get; set; }
    public string? TransactionType { get; set; }
    public string SortBy { get; set; } = "TransactionDate";
    public string SortDirection { get; set; } = "desc";

    /// <summary>
    /// Creates TransactionQueryParameters from GetTransactionsQuery
    /// </summary>
    public static TransactionQueryParameters FromGetTransactionsQuery(Features.Transactions.Queries.GetTransactionsQuery query)
    {
        return new TransactionQueryParameters
        {
            UserId = query.UserId,
            Page = query.Page,
            PageSize = query.PageSize,
            AccountId = query.AccountId,
            CategoryId = query.CategoryId,
            StartDate = query.StartDate,
            EndDate = query.EndDate,
            MinAmount = query.MinAmount,
            MaxAmount = query.MaxAmount,
            Status = query.Status,
            SearchTerm = query.SearchTerm,
            IsReviewed = query.IsReviewed,
            IsReconciled = query.IsReconciled,
            IsExcluded = query.IsExcluded,
            NeedsCategorization = query.NeedsCategorization,
            IncludeTransfers = query.IncludeTransfers,
            OnlyTransfers = query.OnlyTransfers,
            TransferId = query.TransferId,
            TransactionType = query.TransactionType,
            SortBy = query.SortBy,
            SortDirection = query.SortDirection
        };
    }
}