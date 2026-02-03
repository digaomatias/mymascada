using MyMascada.Domain.Entities;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Repository interface for managing budgets and budget categories
/// </summary>
public interface IBudgetRepository
{
    // Budget operations

    /// <summary>
    /// Gets all budgets for a user
    /// </summary>
    Task<IEnumerable<Budget>> GetBudgetsForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active budgets for a user
    /// </summary>
    Task<IEnumerable<Budget>> GetActiveBudgetsForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific budget by ID for a user
    /// </summary>
    Task<Budget?> GetBudgetByIdAsync(int budgetId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the budget that contains the specified date for a user
    /// </summary>
    Task<Budget?> GetBudgetForDateAsync(Guid userId, DateTime date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current active budget for a user (based on today's date)
    /// </summary>
    Task<Budget?> GetCurrentBudgetAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new budget
    /// </summary>
    Task<Budget> CreateBudgetAsync(Budget budget, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing budget
    /// </summary>
    Task<Budget> UpdateBudgetAsync(Budget budget, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a budget (soft delete)
    /// </summary>
    Task DeleteBudgetAsync(int budgetId, Guid userId, CancellationToken cancellationToken = default);

    // Budget Category operations

    /// <summary>
    /// Gets all budget categories for a specific budget
    /// </summary>
    Task<IEnumerable<BudgetCategory>> GetBudgetCategoriesAsync(int budgetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific budget category
    /// </summary>
    Task<BudgetCategory?> GetBudgetCategoryAsync(int budgetId, int categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or updates a category allocation in a budget
    /// </summary>
    Task<BudgetCategory> UpsertBudgetCategoryAsync(BudgetCategory budgetCategory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a category from a budget
    /// </summary>
    Task RemoveBudgetCategoryAsync(int budgetId, int categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets budgets that need rollover calculation (previous periods that have ended)
    /// </summary>
    Task<IEnumerable<Budget>> GetBudgetsNeedingRolloverAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a budget name already exists for the user
    /// </summary>
    Task<bool> BudgetNameExistsAsync(Guid userId, string name, int? excludeBudgetId = null, CancellationToken cancellationToken = default);
}
