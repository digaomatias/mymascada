using MyMascada.Application.Features.Transactions.Queries;
using MyMascada.Application.Features.Transactions.DTOs;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Common.Interfaces;

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(int id, Guid userId);
    Task<Transaction?> GetByIdAsync(int id);
    Task<IEnumerable<Transaction>> GetByAccountIdAsync(int accountId, Guid userId);
    Task<bool> HasTransactionsAsync(int accountId, Guid userId);
    Task<IEnumerable<Transaction>> GetByCategoryIdAsync(int categoryId, Guid userId);
    Task<(IEnumerable<Transaction> transactions, int totalCount)> GetFilteredAsync(GetTransactionsQuery query);
    Task<TransactionSummaryDto> GetSummaryAsync(GetTransactionsQuery query);
    Task<Transaction> AddAsync(Transaction transaction);
    Task UpdateAsync(Transaction transaction);
    Task DeleteAsync(Transaction transaction);
    Task DeleteByAccountIdAsync(int accountId, Guid userId);
    Task<decimal> GetAccountBalanceAsync(int accountId, Guid userId);
    Task<IEnumerable<Transaction>> GetRecentTransactionsAsync(Guid userId, int count = 10, CancellationToken cancellationToken = default);
    Task<bool> ExistsByExternalIdAsync(string externalId, int accountId);
    Task<Transaction?> GetByExternalIdAsync(string externalId);
    Task<Transaction?> GetTransactionByExternalIdAsync(string userId, string externalId);
    Task<Transaction?> GetPotentialDuplicateAsync(int accountId, decimal amount, string description, DateTime startWindow, DateTime endWindow);
    Task<Transaction?> GetRecentDuplicateAsync(int accountId, decimal amount, string description, TimeSpan timeWindow);
    Task<IEnumerable<Transaction>> GetRecentAsync(Guid userId, int count = 5);
    Task<IEnumerable<Transaction>> GetByDateRangeAsync(Guid userId, int accountId, DateTime startDate, DateTime endDate, bool excludeReconciled = false);
    Task<IEnumerable<Transaction>> GetByDateRangeAsync(Guid userId, DateTime startDate, DateTime endDate);
    Task<IEnumerable<Transaction>> GetTransactionsByDateRangeAsync(int accountId, DateTime startDate, DateTime endDate, Guid userId);
    Task<int> GetCountByUserIdAsync(Guid userId);
    Task<IEnumerable<Transaction>> GetUnreviewedAsync(Guid userId);
    Task<Dictionary<int, decimal>> GetAccountBalancesAsync(Guid userId);
    Task<IEnumerable<string>> GetUniqueDescriptionsAsync(Guid userId, string? searchTerm = null, int limit = 10);
    Task<(decimal currentMonth, decimal previousMonth)> GetMonthlySpendingAsync(int accountId, Guid userId);
    
    // LLM Categorization support
    Task<IEnumerable<Transaction>> GetTransactionsByIdsAsync(IEnumerable<int> transactionIds, Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Category>> GetCategoriesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<CategorizationRule>> GetCategorizationRulesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Transaction>> GetCategorizedTransactionsAsync(Guid userId, int count = 200, CancellationToken cancellationToken = default);
    Task<IEnumerable<Transaction>> GetUncategorizedTransactionsAsync(Guid userId, int maxCount = 500, CancellationToken cancellationToken = default);

    Task SaveChangesAsync();
    
    // Data integrity methods
    Task<IEnumerable<Transaction>> GetOrphanedTransactionsAsync(Guid userId);
    Task<int> GetOrphanedTransactionCountAsync(Guid userId);
    Task UpdateAccountIdAsync(int oldAccountId, int newAccountId, Guid userId);
    Task HardDeleteTransactionsByAccountIdAsync(int accountId, Guid userId);
    
    // Duplicate detection
    Task<List<Transaction>> GetAllForDuplicateDetectionAsync(Guid userId, bool includeReviewed = false, DateTime? sinceDate = null);

    // Transfer detection
    Task<List<Transaction>> GetUserTransactionsAsync(Guid userId, bool includeDeleted = false, bool includeReviewed = true, bool includeTransfers = true, DateTime? sinceDate = null);
    Task<List<Transaction>> GetByTransferIdAsync(Guid transferId, Guid userId);
    
    // Amount normalization
    Task<IEnumerable<Transaction>> GetAllTransactionsForNormalizationAsync();
    
    // Categorization status checking
    Task<HashSet<int>> GetCategorizedTransactionIdsAsync(IEnumerable<int> transactionIds);
    
    // Bulk operations for categorization
    Task BulkUpdateCategorizationAsync<T>(IEnumerable<T> updates, Guid userId, CancellationToken cancellationToken = default)
        where T : class;
}

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(int id, Guid userId);
    Task<IEnumerable<Account>> GetByUserIdAsync(Guid userId);
    Task<Account> AddAsync(Account account);
    Task UpdateAsync(Account account);
    Task DeleteAsync(Account account);
    Task<bool> ExistsAsync(int id, Guid userId);
    
    // Data integrity methods
    Task<IEnumerable<Account>> GetSoftDeletedAccountsAsync(Guid userId);
    Task<IEnumerable<Account>> GetSoftDeletedAccountsWithTransactionsAsync(Guid userId);
    Task RestoreAccountAsync(int accountId, Guid userId);
    Task<Account?> GetByIdIncludingDeletedAsync(int id, Guid userId);
}

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(int id);
    Task<IEnumerable<Category>> GetByUserIdAsync(Guid userId);
    Task<IEnumerable<Category>> GetSystemCategoriesAsync();
    Task<Category?> GetByNameAsync(string name, Guid userId, bool includeInactive = false);
    Task<Category> AddAsync(Category category);
    Task UpdateAsync(Category category);
    Task DeleteAsync(Category category);
    Task<bool> ExistsAsync(int id, Guid? userId = null);
    Task<IEnumerable<CategoryWithTransactionCount>> GetCategoriesWithTransactionCountsAsync(
        Guid userId,
        int? accountId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        decimal? minAmount = null,
        decimal? maxAmount = null,
        TransactionStatus? status = null,
        string? searchTerm = null,
        bool? isReviewed = null,
        bool? isExcluded = null,
        bool? includeTransfers = null,
        bool? onlyTransfers = null,
        Guid? transferId = null);

    // Batch operations for canonical key backfill
    Task<IEnumerable<Category>> GetCategoriesWithNullCanonicalKeyAsync();
    Task SaveChangesAsync();
}

public interface ITransferRepository
{
    Task<Transfer?> GetByIdAsync(int id, Guid userId);
    Task<Transfer?> GetByIdAsync(Guid transferId, Guid userId);
    Task<Transfer?> GetByTransferIdAsync(Guid transferId, Guid userId);
    Task<IEnumerable<Transfer>> GetByUserIdAsync(Guid userId);
    Task<(IEnumerable<Transfer> transfers, int totalCount)> GetFilteredAsync(
        Guid userId,
        int page = 1,
        int pageSize = 50,
        int? sourceAccountId = null,
        int? destinationAccountId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        decimal? minAmount = null,
        decimal? maxAmount = null,
        TransferStatus? status = null,
        string sortBy = "TransferDate",
        string sortDirection = "desc");
    Task<Transfer> AddAsync(Transfer transfer);
    Task UpdateAsync(Transfer transfer);
    Task DeleteAsync(Transfer transfer);
    Task<bool> ExistsByTransferIdAsync(Guid transferId);
    Task<IEnumerable<Transfer>> GetRecentAsync(Guid userId, int count = 10);
    Task SaveChangesAsync();
}
