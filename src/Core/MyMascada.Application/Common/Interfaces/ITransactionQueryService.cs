using MyMascada.Application.Common.Models;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Service for building consistent transaction queries across different contexts
/// </summary>
public interface ITransactionQueryService
{
    /// <summary>
    /// Builds a base transaction query with all common filters applied
    /// </summary>
    /// <param name="parameters">Query parameters</param>
    /// <returns>IQueryable with filters applied but no pagination or ordering</returns>
    Task<IQueryable<Transaction>> BuildTransactionQueryAsync(TransactionQueryParameters parameters);

    /// <summary>
    /// Applies sorting to a transaction query
    /// </summary>
    /// <param name="query">The base query</param>
    /// <param name="sortBy">Field to sort by</param>
    /// <param name="sortDirection">Direction (asc/desc)</param>
    /// <returns>Query with sorting applied</returns>
    IQueryable<Transaction> ApplySorting(IQueryable<Transaction> query, string sortBy, string sortDirection);

    /// <summary>
    /// Applies pagination to a transaction query
    /// </summary>
    /// <param name="query">The sorted query</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Items per page</param>
    /// <returns>Query with pagination applied</returns>
    IQueryable<Transaction> ApplyPagination(IQueryable<Transaction> query, int page, int pageSize);

    /// <summary>
    /// Gets transaction IDs that match the query parameters (for joining with other tables)
    /// </summary>
    /// <param name="parameters">Query parameters</param>
    /// <returns>List of transaction IDs</returns>
    Task<List<int>> GetTransactionIdsAsync(TransactionQueryParameters parameters);
}