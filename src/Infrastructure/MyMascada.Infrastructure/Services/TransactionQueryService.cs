using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Common.Models;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Services;

/// <summary>
/// Service for building consistent transaction queries across different contexts
/// </summary>
public class TransactionQueryService : ITransactionQueryService
{
    private readonly ApplicationDbContext _context;

    public TransactionQueryService(ApplicationDbContext context)
    {
        _context = context;
    }

    public IQueryable<Transaction> BuildTransactionQuery(TransactionQueryParameters parameters)
    {
        var query = _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .Where(t => t.Account.UserId == parameters.UserId && !t.Account.IsDeleted);

        // Apply filters using the same logic as TransactionRepository
        if (parameters.AccountId.HasValue)
            query = query.Where(t => t.AccountId == parameters.AccountId.Value);

        if (parameters.CategoryId.HasValue)
            query = query.Where(t => t.CategoryId == parameters.CategoryId.Value);

        if (parameters.StartDate.HasValue)
            query = query.Where(t => t.TransactionDate >= parameters.StartDate.Value);

        if (parameters.EndDate.HasValue)
            query = query.Where(t => t.TransactionDate <= parameters.EndDate.Value);

        if (parameters.MinAmount.HasValue)
            query = query.Where(t => Math.Abs(t.Amount) >= parameters.MinAmount.Value);

        if (parameters.MaxAmount.HasValue)
            query = query.Where(t => Math.Abs(t.Amount) <= parameters.MaxAmount.Value);

        if (parameters.Status.HasValue)
            query = query.Where(t => t.Status == parameters.Status.Value);

        if (parameters.IsReviewed.HasValue)
            query = query.Where(t => t.IsReviewed == parameters.IsReviewed.Value);

        if (parameters.IsReconciled.HasValue)
        {
            if (parameters.IsReconciled.Value)
                query = query.Where(t => t.Status == Domain.Enums.TransactionStatus.Reconciled);
            else
                query = query.Where(t => t.Status != Domain.Enums.TransactionStatus.Reconciled);
        }

        if (parameters.IsExcluded.HasValue)
            query = query.Where(t => t.IsExcluded == parameters.IsExcluded.Value);

        if (parameters.NeedsCategorization.HasValue && parameters.NeedsCategorization.Value)
            query = query.Where(t => !t.IsReviewed && !t.TransferId.HasValue);

        // Transfer filtering
        if (parameters.TransferId.HasValue)
            query = query.Where(t => t.TransferId == parameters.TransferId.Value);

        if (parameters.OnlyTransfers.HasValue && parameters.OnlyTransfers.Value)
            query = query.Where(t => t.TransferId.HasValue);

        if (parameters.IncludeTransfers.HasValue && !parameters.IncludeTransfers.Value)
            query = query.Where(t => !t.TransferId.HasValue);

        // Search term filtering
        if (!string.IsNullOrEmpty(parameters.SearchTerm))
        {
            var searchTerm = parameters.SearchTerm.ToLower();
            query = query.Where(t => 
                t.Description.ToLower().Contains(searchTerm) ||
                (t.UserDescription != null && t.UserDescription.ToLower().Contains(searchTerm)) ||
                (t.Notes != null && t.Notes.ToLower().Contains(searchTerm)) ||
                (t.Location != null && t.Location.ToLower().Contains(searchTerm)) ||
                (t.Category != null && t.Category.Name.ToLower().Contains(searchTerm)) ||
                t.Account.Name.ToLower().Contains(searchTerm));
        }

        // Transaction type filtering (income/expense)
        if (!string.IsNullOrEmpty(parameters.TransactionType))
        {
            switch (parameters.TransactionType.ToLower())
            {
                case "income":
                    query = query.Where(t => t.Amount > 0);
                    break;
                case "expense":
                    query = query.Where(t => t.Amount < 0);
                    break;
            }
        }

        return query;
    }

    public IQueryable<Transaction> ApplySorting(IQueryable<Transaction> query, string sortBy, string sortDirection)
    {
        var isDescending = sortDirection.ToLower() == "desc";

        return sortBy.ToLower() switch
        {
            "transactiondate" => isDescending 
                ? query.OrderByDescending(t => t.TransactionDate)
                : query.OrderBy(t => t.TransactionDate),
            "amount" => isDescending 
                ? query.OrderByDescending(t => Math.Abs(t.Amount))
                : query.OrderBy(t => Math.Abs(t.Amount)),
            "description" => isDescending 
                ? query.OrderByDescending(t => t.Description)
                : query.OrderBy(t => t.Description),
            "category" => isDescending 
                ? query.OrderByDescending(t => t.Category != null ? t.Category.Name : "")
                : query.OrderBy(t => t.Category != null ? t.Category.Name : ""),
            "account" => isDescending 
                ? query.OrderByDescending(t => t.Account.Name)
                : query.OrderBy(t => t.Account.Name),
            _ => isDescending 
                ? query.OrderByDescending(t => t.TransactionDate)
                : query.OrderBy(t => t.TransactionDate)
        };
    }

    public IQueryable<Transaction> ApplyPagination(IQueryable<Transaction> query, int page, int pageSize)
    {
        return query
            .Skip((page - 1) * pageSize)
            .Take(pageSize);
    }

    public async Task<List<int>> GetTransactionIdsAsync(TransactionQueryParameters parameters)
    {
        var query = BuildTransactionQuery(parameters);
        var sortedQuery = ApplySorting(query, parameters.SortBy, parameters.SortDirection);
        var paginatedQuery = ApplyPagination(sortedQuery, parameters.Page, parameters.PageSize);

        return await paginatedQuery
            .Select(t => t.Id)
            .ToListAsync();
    }
}