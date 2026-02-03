using MyMascada.Domain.Entities;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Repository interface for BankCategoryMapping data access.
/// </summary>
public interface IBankCategoryMappingRepository
{
    /// <summary>
    /// Gets a mapping by its ID.
    /// </summary>
    Task<BankCategoryMapping?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Gets a mapping by its ID and verifies user ownership.
    /// </summary>
    Task<BankCategoryMapping?> GetByIdAsync(int id, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Gets a mapping by bank category name, provider, and user.
    /// Uses normalized name for matching.
    /// </summary>
    Task<BankCategoryMapping?> GetByBankCategoryAsync(
        string normalizedBankCategoryName,
        string providerId,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all mappings for a user, optionally filtered by provider.
    /// </summary>
    Task<IEnumerable<BankCategoryMapping>> GetByUserIdAsync(
        Guid userId,
        string? providerId = null,
        bool activeOnly = true,
        CancellationToken ct = default);

    /// <summary>
    /// Gets mappings for multiple bank category names in a batch.
    /// </summary>
    Task<IEnumerable<BankCategoryMapping>> GetBatchByBankCategoriesAsync(
        IEnumerable<string> normalizedBankCategoryNames,
        string providerId,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Adds a new mapping.
    /// </summary>
    Task<BankCategoryMapping> AddAsync(BankCategoryMapping mapping, CancellationToken ct = default);

    /// <summary>
    /// Adds multiple mappings.
    /// </summary>
    Task<IEnumerable<BankCategoryMapping>> AddRangeAsync(
        IEnumerable<BankCategoryMapping> mappings,
        CancellationToken ct = default);

    /// <summary>
    /// Updates an existing mapping.
    /// </summary>
    Task UpdateAsync(BankCategoryMapping mapping, CancellationToken ct = default);

    /// <summary>
    /// Soft deletes a mapping.
    /// </summary>
    Task DeleteAsync(BankCategoryMapping mapping, CancellationToken ct = default);

    /// <summary>
    /// Increments the application count for a mapping.
    /// </summary>
    Task IncrementApplicationCountAsync(int mappingId, CancellationToken ct = default);

    /// <summary>
    /// Increments the override count for a mapping.
    /// </summary>
    Task IncrementOverrideCountAsync(int mappingId, CancellationToken ct = default);

    /// <summary>
    /// Gets all mappings that reference a specific category.
    /// Used when a category is deleted or modified.
    /// </summary>
    Task<IEnumerable<BankCategoryMapping>> GetByCategoryIdAsync(
        int categoryId,
        CancellationToken ct = default);

    /// <summary>
    /// Saves all pending changes.
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
