using MyMascada.Application.Features.Budgets.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Budgets.Services;

/// <summary>
/// Service for calculating budget progress and generating budget suggestions
/// </summary>
public interface IBudgetCalculationService
{
    /// <summary>
    /// Calculates full budget progress including all category spending
    /// </summary>
    Task<BudgetDetailDto> CalculateBudgetProgressAsync(
        Budget budget,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates spending for a single category with optional subcategory rollup
    /// </summary>
    Task<CategorySpendingSummaryDto> GetCategorySpendingAsync(
        int categoryId,
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        bool includeSubcategories = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates spending for multiple categories in a single batch operation
    /// </summary>
    Task<Dictionary<int, CategorySpendingSummaryDto>> GetCategorySpendingBatchAsync(
        IEnumerable<int> categoryIds,
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        bool includeSubcategories = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates budget suggestions based on historical spending patterns
    /// </summary>
    Task<List<BudgetSuggestionDto>> GenerateBudgetSuggestionsAsync(
        Guid userId,
        int monthsToAnalyze = 3,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts a Budget entity to a summary DTO with calculated progress
    /// </summary>
    Task<BudgetSummaryDto> ToBudgetSummaryAsync(
        Budget budget,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Projects end-of-period spending based on current pace
    /// </summary>
    decimal ProjectEndOfPeriodSpending(
        decimal currentSpent,
        int daysElapsed,
        int totalDays);
}
