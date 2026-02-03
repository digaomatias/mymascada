using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Common.Interfaces;

public interface IReconciliationRepository
{
    Task<Reconciliation?> GetByIdAsync(int id, Guid userId);
    Task<IEnumerable<Reconciliation>> GetByAccountIdAsync(int accountId, Guid userId);
    Task<(IEnumerable<Reconciliation> reconciliations, int totalCount)> GetFilteredAsync(
        Guid userId,
        int page = 1,
        int pageSize = 25,
        int? accountId = null,
        ReconciliationStatus? status = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string sortBy = "ReconciliationDate",
        string sortDirection = "desc");
    Task<Reconciliation> AddAsync(Reconciliation reconciliation);
    Task UpdateAsync(Reconciliation reconciliation);
    Task DeleteAsync(Reconciliation reconciliation);
    Task<bool> ExistsAsync(int id, Guid userId);
    Task<Reconciliation?> GetLatestByAccountAsync(int accountId, Guid userId);
    Task<IEnumerable<Reconciliation>> GetRecentAsync(Guid userId, int count = 10);
    Task<int> GetCountByAccountAsync(int accountId, Guid userId);
    Task<decimal?> GetLastReconciledBalanceAsync(int accountId, Guid userId);
    Task SaveChangesAsync();
}

public interface IReconciliationItemRepository
{
    Task<ReconciliationItem?> GetByIdAsync(int id, Guid userId);
    Task<IEnumerable<ReconciliationItem>> GetByReconciliationIdAsync(int reconciliationId, Guid userId);
    Task<IEnumerable<ReconciliationItem>> GetByTransactionIdAsync(int transactionId, Guid userId);
    Task<(IEnumerable<ReconciliationItem> items, int totalCount)> GetFilteredAsync(
        Guid userId,
        int reconciliationId,
        int page = 1,
        int pageSize = 25,
        ReconciliationItemType? itemType = null,
        decimal? minConfidence = null,
        MatchMethod? matchMethod = null,
        string sortBy = "CreatedAt",
        string sortDirection = "desc");
    Task<ReconciliationItem> AddAsync(ReconciliationItem item);
    Task<IEnumerable<ReconciliationItem>> AddRangeAsync(IEnumerable<ReconciliationItem> items);
    Task UpdateAsync(ReconciliationItem item);
    Task DeleteAsync(ReconciliationItem item);
    Task DeleteByReconciliationIdAsync(int reconciliationId, Guid userId);
    Task<bool> IsTransactionReconciledAsync(int transactionId, Guid userId);
    Task<ReconciliationItem?> GetByTransactionAndReconciliationAsync(int transactionId, int reconciliationId, Guid userId);
    Task<int> GetUnmatchedCountAsync(int reconciliationId, Guid userId);
    Task<int> GetMatchedCountAsync(int reconciliationId, Guid userId);
    Task<decimal> GetMatchedPercentageAsync(int reconciliationId, Guid userId);
    Task SaveChangesAsync();
}

public interface IReconciliationAuditLogRepository
{
    Task<ReconciliationAuditLog?> GetByIdAsync(int id, Guid userId);
    Task<IEnumerable<ReconciliationAuditLog>> GetByReconciliationIdAsync(int reconciliationId, Guid userId);
    Task<(IEnumerable<ReconciliationAuditLog> logs, int totalCount)> GetFilteredAsync(
        Guid userId,
        int? reconciliationId = null,
        ReconciliationAction? action = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 25,
        string sortBy = "Timestamp",
        string sortDirection = "desc");
    Task<ReconciliationAuditLog> AddAsync(ReconciliationAuditLog log);
    Task<IEnumerable<ReconciliationAuditLog>> AddRangeAsync(IEnumerable<ReconciliationAuditLog> logs);
    Task<IEnumerable<ReconciliationAuditLog>> GetRecentByUserAsync(Guid userId, int count = 10);
    Task<int> GetCountByReconciliationAsync(int reconciliationId, Guid userId);
    Task SaveChangesAsync();
}