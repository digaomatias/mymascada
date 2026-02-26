using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Budgets.DTOs;
using MyMascada.Domain.Common;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Budgets.Services;

/// <summary>
/// Service for calculating budget progress and generating budget suggestions
/// </summary>
public class BudgetCalculationService : IBudgetCalculationService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategoryRepository _categoryRepository;

    public BudgetCalculationService(
        ITransactionRepository transactionRepository,
        ICategoryRepository categoryRepository)
    {
        _transactionRepository = transactionRepository;
        _categoryRepository = categoryRepository;
    }

    public async Task<BudgetDetailDto> CalculateBudgetProgressAsync(
        Budget budget,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var periodStart = budget.StartDate;
        var periodEnd = budget.GetPeriodEndDate();
        var today = DateTimeProvider.UtcNow;

        // Get all category IDs that need spending calculated
        var categoryIds = budget.BudgetCategories
            .Where(bc => !bc.IsDeleted)
            .Select(bc => bc.CategoryId)
            .ToList();

        // Batch calculate spending for all categories
        var spendingByCategory = await GetCategorySpendingBatchAsync(
            categoryIds,
            userId,
            periodStart,
            periodEnd,
            includeSubcategories: true, // We'll handle per-category setting below
            cancellationToken);

        // Build category progress list
        var categoryProgress = new List<BudgetCategoryProgressDto>();
        decimal totalBudgeted = 0;
        decimal totalSpent = 0;

        foreach (var bc in budget.BudgetCategories.Where(bc => !bc.IsDeleted))
        {
            var spending = spendingByCategory.GetValueOrDefault(bc.CategoryId);

            // If subcategories not included, we need to recalculate
            CategorySpendingSummaryDto actualSpending;
            if (!bc.IncludeSubcategories && spending != null)
            {
                actualSpending = await GetCategorySpendingAsync(
                    bc.CategoryId,
                    userId,
                    periodStart,
                    periodEnd,
                    includeSubcategories: false,
                    cancellationToken);
            }
            else
            {
                actualSpending = spending ?? new CategorySpendingSummaryDto
                {
                    CategoryId = bc.CategoryId,
                    CategoryName = bc.Category?.Name ?? "Unknown",
                    TotalSpent = 0,
                    TransactionCount = 0
                };
            }

            var effectiveBudget = bc.GetEffectiveBudget();
            var remaining = bc.GetRemainingBudget(actualSpending.TotalSpent);
            var usedPercentage = bc.GetUsedPercentage(actualSpending.TotalSpent);
            var isOver = bc.IsOverBudget(actualSpending.TotalSpent);
            var isApproaching = bc.IsApproachingLimit(actualSpending.TotalSpent);

            categoryProgress.Add(new BudgetCategoryProgressDto
            {
                CategoryId = bc.CategoryId,
                CategoryName = bc.Category?.Name ?? "Unknown",
                CategoryColor = bc.Category?.Color,
                CategoryIcon = bc.Category?.Icon,
                ParentCategoryName = bc.Category?.ParentCategory?.Name,
                BudgetedAmount = bc.BudgetedAmount,
                RolloverAmount = bc.RolloverAmount ?? 0,
                EffectiveBudget = effectiveBudget,
                ActualSpent = actualSpending.TotalSpent,
                RemainingAmount = remaining,
                UsedPercentage = usedPercentage,
                IsOverBudget = isOver,
                IsApproachingLimit = isApproaching,
                TransactionCount = actualSpending.TransactionCount,
                AllowRollover = bc.AllowRollover,
                IncludeSubcategories = bc.IncludeSubcategories
            });

            totalBudgeted += effectiveBudget;
            totalSpent += actualSpending.TotalSpent;
        }

        // Sort by used percentage descending (most overspent first)
        categoryProgress = categoryProgress
            .OrderByDescending(c => c.UsedPercentage)
            .ToList();

        var totalRemaining = totalBudgeted - totalSpent;
        var overallUsedPercentage = totalBudgeted > 0
            ? Math.Round(totalSpent / totalBudgeted * 100, 1)
            : 0;

        return new BudgetDetailDto
        {
            Id = budget.Id,
            Name = budget.Name,
            Description = budget.Description,
            PeriodType = budget.PeriodType.ToString(),
            StartDate = periodStart,
            EndDate = periodEnd,
            IsRecurring = budget.IsRecurring,
            IsActive = budget.IsActive,
            TotalBudgeted = totalBudgeted,
            TotalSpent = totalSpent,
            TotalRemaining = totalRemaining,
            UsedPercentage = overallUsedPercentage,
            DaysRemaining = budget.GetDaysRemaining(),
            TotalDays = budget.GetTotalDays(),
            PeriodElapsedPercentage = budget.GetPeriodElapsedPercentage(),
            Categories = categoryProgress
        };
    }

    public async Task<CategorySpendingSummaryDto> GetCategorySpendingAsync(
        int categoryId,
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        bool includeSubcategories = true,
        CancellationToken cancellationToken = default)
    {
        // Get the category with its hierarchy
        var category = await _categoryRepository.GetByIdAsync(categoryId);
        if (category == null)
        {
            return new CategorySpendingSummaryDto
            {
                CategoryId = categoryId,
                CategoryName = "Unknown",
                TotalSpent = 0,
                TransactionCount = 0
            };
        }

        // Determine which category IDs to include
        var categoryIds = new HashSet<int> { categoryId };
        if (includeSubcategories && category.SubCategories.Any())
        {
            var descendants = category.GetAllDescendants();
            foreach (var desc in descendants)
            {
                categoryIds.Add(desc.Id);
            }
        }

        // Get transactions for the period
        var transactions = await _transactionRepository.GetByDateRangeAsync(
            userId, startDate, endDate);

        // Filter and sum expenses for these categories
        // Expenses are negative amounts, transfers excluded
        var relevantTransactions = transactions
            .Where(t => t.Amount < 0
                        && t.CategoryId.HasValue
                        && categoryIds.Contains(t.CategoryId.Value)
                        && !t.TransferId.HasValue)
            .ToList();

        var totalSpent = relevantTransactions.Sum(t => Math.Abs(t.Amount));

        return new CategorySpendingSummaryDto
        {
            CategoryId = categoryId,
            CategoryName = category.Name,
            CategoryColor = category.Color,
            TotalSpent = totalSpent,
            TransactionCount = relevantTransactions.Count,
            IncludedCategoryIds = categoryIds.ToList()
        };
    }

    public async Task<Dictionary<int, CategorySpendingSummaryDto>> GetCategorySpendingBatchAsync(
        IEnumerable<int> categoryIds,
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        bool includeSubcategories = true,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<int, CategorySpendingSummaryDto>();
        var categoryIdList = categoryIds.ToList();

        if (!categoryIdList.Any())
            return result;

        // Get all categories for the user to build hierarchy
        var allCategories = (await _categoryRepository.GetByUserIdAsync(userId)).ToList();
        var categoryLookup = allCategories.ToDictionary(c => c.Id);

        // Build mapping of each budget category to all included category IDs
        var categoryToIncludedIds = new Dictionary<int, HashSet<int>>();
        foreach (var catId in categoryIdList)
        {
            var includedIds = new HashSet<int> { catId };

            if (includeSubcategories && categoryLookup.TryGetValue(catId, out var category))
            {
                // Get all descendant IDs
                var descendants = GetAllDescendantIds(catId, allCategories);
                foreach (var descId in descendants)
                {
                    includedIds.Add(descId);
                }
            }

            categoryToIncludedIds[catId] = includedIds;
        }

        // Get all transactions for the period in a single query
        var transactions = await _transactionRepository.GetByDateRangeAsync(
            userId, startDate, endDate);

        // Filter to expenses only (negative amounts, no transfers)
        var expenses = transactions
            .Where(t => t.Amount < 0 && t.CategoryId.HasValue && !t.TransferId.HasValue)
            .ToList();

        // Group by category ID for efficient lookup
        var transactionsByCategory = expenses
            .GroupBy(t => t.CategoryId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Calculate spending for each budget category
        foreach (var catId in categoryIdList)
        {
            var includedIds = categoryToIncludedIds[catId];
            var relevantTransactions = new List<Transaction>();

            foreach (var includedId in includedIds)
            {
                if (transactionsByCategory.TryGetValue(includedId, out var catTransactions))
                {
                    relevantTransactions.AddRange(catTransactions);
                }
            }

            var category = categoryLookup.GetValueOrDefault(catId);
            var totalSpent = relevantTransactions.Sum(t => Math.Abs(t.Amount));

            result[catId] = new CategorySpendingSummaryDto
            {
                CategoryId = catId,
                CategoryName = category?.Name ?? "Unknown",
                CategoryColor = category?.Color,
                TotalSpent = totalSpent,
                TransactionCount = relevantTransactions.Count,
                IncludedCategoryIds = includedIds.ToList()
            };
        }

        return result;
    }

    public async Task<List<BudgetSuggestionDto>> GenerateBudgetSuggestionsAsync(
        Guid userId,
        int monthsToAnalyze = 3,
        CancellationToken cancellationToken = default)
    {
        var suggestions = new List<BudgetSuggestionDto>();

        // Get all expense categories for the user
        var categories = (await _categoryRepository.GetByUserIdAsync(userId))
            .Where(c => !c.IsDeleted)
            .ToList();

        // Build category lookup for parent names
        var categoryLookup = categories.ToDictionary(c => c.Id);

        // Calculate date range for analysis
        var endDate = DateTimeProvider.UtcNow;
        var startDate = endDate.AddMonths(-monthsToAnalyze);

        // Get all transactions for the period
        var transactions = await _transactionRepository.GetByDateRangeAsync(
            userId, startDate, endDate);

        // Filter to categorized expenses
        var expenses = transactions
            .Where(t => t.Amount < 0 && t.CategoryId.HasValue && !t.TransferId.HasValue)
            .ToList();

        // Calculate total expenses for percentage calculation
        var totalExpenses = expenses.Sum(t => Math.Abs(t.Amount));

        // Group spending by category and month (ordered by date)
        var spendingByCategory = expenses
            .GroupBy(t => t.CategoryId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
                      .OrderBy(mg => mg.Key.Year)
                      .ThenBy(mg => mg.Key.Month)
                      .Select(mg => new MonthlySpendingData
                      {
                          Year = mg.Key.Year,
                          Month = mg.Key.Month,
                          Total = mg.Sum(t => Math.Abs(t.Amount)),
                          Count = mg.Count()
                      })
                      .ToList());

        foreach (var category in categories)
        {
            if (!spendingByCategory.TryGetValue(category.Id, out var monthlySpending))
                continue;

            if (!monthlySpending.Any())
                continue;

            var monthlyAmounts = monthlySpending.Select(m => m.Total).ToList();
            var averageSpending = monthlyAmounts.Average();
            var minSpending = monthlyAmounts.Min();
            var maxSpending = monthlyAmounts.Max();
            var totalTransactions = monthlySpending.Sum(m => m.Count);
            var lastMonthSpending = monthlySpending.Last().Total;
            var categoryTotalSpent = monthlyAmounts.Sum();

            // Calculate confidence based on consistency
            var variance = monthlyAmounts.Count > 1
                ? monthlyAmounts.Select(m => Math.Pow((double)(m - averageSpending), 2)).Average()
                : 0;
            var stdDev = (decimal)Math.Sqrt(variance);
            var coefficientOfVariation = averageSpending > 0 ? stdDev / averageSpending : 0;
            var confidence = Math.Max(0, Math.Min(1, 1 - coefficientOfVariation));

            // Calculate trend analysis
            var (trendDirection, trendPercentage, projectedNextMonth) = CalculateSpendingTrend(monthlySpending);

            // Calculate percentage of total expenses
            var percentageOfTotal = totalExpenses > 0 ? categoryTotalSpent / totalExpenses * 100 : 0;

            // Determine recommendation type and priority
            var (recommendationType, priorityScore) = DetermineRecommendationType(
                averageSpending, confidence, trendDirection, trendPercentage, percentageOfTotal);

            // Generate insight
            var insight = GenerateSpendingInsight(
                category.Name, averageSpending, trendDirection, trendPercentage,
                confidence, monthlySpending.Count, percentageOfTotal);

            // Round suggestion to a friendly number
            var suggestedBudget = RoundToFriendlyNumber(averageSpending);

            // Get parent category name
            string? parentCategoryName = null;
            if (category.ParentCategoryId.HasValue &&
                categoryLookup.TryGetValue(category.ParentCategoryId.Value, out var parentCategory))
            {
                parentCategoryName = parentCategory.Name;
            }

            suggestions.Add(new BudgetSuggestionDto
            {
                CategoryId = category.Id,
                CategoryName = category.Name,
                CategoryColor = category.Color,
                CategoryIcon = category.Icon,
                ParentCategoryName = parentCategoryName,
                AverageMonthlySpending = Math.Round(averageSpending, 2),
                SuggestedBudget = suggestedBudget,
                MinSpending = Math.Round(minSpending, 2),
                MaxSpending = Math.Round(maxSpending, 2),
                MonthsAnalyzed = monthlySpending.Count,
                TotalTransactionCount = totalTransactions,
                Confidence = Math.Round(confidence, 2),
                // Enhanced fields
                SpendingTrend = trendDirection,
                TrendPercentage = Math.Round(trendPercentage, 1),
                ProjectedNextMonth = Math.Round(projectedNextMonth, 2),
                PriorityScore = priorityScore,
                RecommendationType = recommendationType,
                Insight = insight,
                PercentageOfTotal = Math.Round(percentageOfTotal, 1),
                LastMonthSpending = Math.Round(lastMonthSpending, 2)
            });
        }

        // Sort by priority score descending, then by average spending
        return suggestions
            .OrderByDescending(s => s.PriorityScore)
            .ThenByDescending(s => s.AverageMonthlySpending)
            .ToList();
    }

    /// <summary>
    /// Helper class for monthly spending data
    /// </summary>
    private class MonthlySpendingData
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Total { get; set; }
        public int Count { get; set; }
    }

    /// <summary>
    /// Calculates the spending trend using linear regression
    /// </summary>
    private static (string direction, decimal percentage, decimal projected) CalculateSpendingTrend(
        List<MonthlySpendingData> monthlyData)
    {
        if (monthlyData.Count < 2)
        {
            var singleAmount = monthlyData.FirstOrDefault()?.Total ?? 0;
            return ("Stable", 0, singleAmount);
        }

        // Simple linear regression to find trend
        var n = monthlyData.Count;
        var amounts = monthlyData.Select(m => m.Total).ToList();

        // x values: 0, 1, 2, ... (month indices)
        var sumX = (decimal)Enumerable.Range(0, n).Sum();
        var sumY = amounts.Sum();
        var sumXY = Enumerable.Range(0, n).Select(i => i * amounts[i]).Sum();
        var sumX2 = (decimal)Enumerable.Range(0, n).Sum(i => i * i);

        // Calculate slope
        var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        var average = sumY / n;

        // Calculate trend percentage (slope as percentage of average)
        var trendPercentage = average > 0 ? (slope / average) * 100 : 0;

        // Determine direction based on significance
        string direction;
        if (Math.Abs(trendPercentage) < 5)
            direction = "Stable";
        else if (trendPercentage > 0)
            direction = "Increasing";
        else
            direction = "Decreasing";

        // Project next month using the trend line
        var intercept = (sumY - slope * sumX) / n;
        var projected = intercept + slope * n; // Next month is index n
        projected = Math.Max(0, projected); // Don't project negative spending

        return (direction, trendPercentage, projected);
    }

    /// <summary>
    /// Determines the recommendation type and priority score
    /// </summary>
    private static (string type, int score) DetermineRecommendationType(
        decimal averageSpending,
        decimal confidence,
        string trendDirection,
        decimal trendPercentage,
        decimal percentageOfTotal)
    {
        var score = 50; // Base score

        // High spending categories are more important to budget
        if (percentageOfTotal >= 20) score += 25;
        else if (percentageOfTotal >= 10) score += 15;
        else if (percentageOfTotal >= 5) score += 10;

        // Consistent spending is easier to budget
        if (confidence >= 0.8m) score += 15;
        else if (confidence >= 0.6m) score += 10;
        else if (confidence < 0.4m) score -= 10;

        // Increasing trends need attention
        if (trendDirection == "Increasing" && trendPercentage > 10)
        {
            score += 15;
        }

        // Determine type
        string type;
        if (percentageOfTotal >= 15 && confidence >= 0.7m)
        {
            type = "Essential";
            score += 10;
        }
        else if (trendDirection == "Decreasing" && trendPercentage < -10)
        {
            type = "SavingsOpportunity";
            score += 5;
        }
        else if (confidence < 0.5m || percentageOfTotal < 3)
        {
            type = "Discretionary";
            score -= 5;
        }
        else
        {
            type = "Regular";
        }

        // Clamp score between 0 and 100
        score = Math.Max(0, Math.Min(100, score));

        return (type, score);
    }

    /// <summary>
    /// Generates a human-readable insight about the spending pattern
    /// </summary>
    private static string GenerateSpendingInsight(
        string categoryName,
        decimal averageSpending,
        string trendDirection,
        decimal trendPercentage,
        decimal confidence,
        int monthsAnalyzed,
        decimal percentageOfTotal)
    {
        var insights = new List<string>();

        // Percentage of total insight
        if (percentageOfTotal >= 20)
            insights.Add($"This is your largest expense category at {percentageOfTotal:F0}% of total spending.");
        else if (percentageOfTotal >= 10)
            insights.Add($"Represents {percentageOfTotal:F0}% of your total expenses.");

        // Trend insight
        if (trendDirection == "Increasing" && trendPercentage > 15)
            insights.Add($"Spending has been increasing significantly (+{trendPercentage:F0}% trend).");
        else if (trendDirection == "Increasing" && trendPercentage > 5)
            insights.Add($"Spending is gradually increasing (+{trendPercentage:F0}% trend).");
        else if (trendDirection == "Decreasing" && trendPercentage < -15)
            insights.Add($"Good news! Spending has decreased significantly ({trendPercentage:F0}% trend).");
        else if (trendDirection == "Decreasing" && trendPercentage < -5)
            insights.Add($"Spending is trending down ({trendPercentage:F0}% trend).");

        // Consistency insight
        if (confidence >= 0.9m)
            insights.Add("Very consistent spending - easy to predict and budget.");
        else if (confidence >= 0.7m)
            insights.Add("Fairly consistent spending pattern.");
        else if (confidence < 0.4m)
            insights.Add("Highly variable spending - consider a flexible budget.");

        // Data quality insight
        if (monthsAnalyzed < 3)
            insights.Add($"Based on only {monthsAnalyzed} month(s) of data - more history will improve accuracy.");

        return insights.Count > 0 ? string.Join(" ", insights) : "Regular spending category.";
    }

    public async Task<BudgetSummaryDto> ToBudgetSummaryAsync(
        Budget budget,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var periodStart = budget.StartDate;
        var periodEnd = budget.GetPeriodEndDate();
        var today = DateTimeProvider.UtcNow;

        // Get category IDs for spending calculation
        var categoryIds = budget.BudgetCategories
            .Where(bc => !bc.IsDeleted)
            .Select(bc => bc.CategoryId)
            .ToList();

        // Calculate total spending (respecting per-category IncludeSubcategories setting)
        decimal totalSpent = 0;
        if (categoryIds.Any())
        {
            var spending = await GetCategorySpendingBatchAsync(
                categoryIds,
                userId,
                periodStart,
                periodEnd,
                includeSubcategories: true,
                cancellationToken);

            foreach (var bc in budget.BudgetCategories.Where(bc => !bc.IsDeleted))
            {
                var catSpending = spending.GetValueOrDefault(bc.CategoryId);
                if (!bc.IncludeSubcategories && catSpending != null)
                {
                    var actualSpending = await GetCategorySpendingAsync(
                        bc.CategoryId,
                        userId,
                        periodStart,
                        periodEnd,
                        includeSubcategories: false,
                        cancellationToken);
                    totalSpent += actualSpending.TotalSpent;
                }
                else
                {
                    totalSpent += catSpending?.TotalSpent ?? 0;
                }
            }
        }

        var totalBudgeted = budget.GetTotalBudgetedAmount();
        var totalRemaining = totalBudgeted - totalSpent;
        var usedPercentage = totalBudgeted > 0
            ? Math.Round(totalSpent / totalBudgeted * 100, 1)
            : 0;

        return new BudgetSummaryDto
        {
            Id = budget.Id,
            Name = budget.Name,
            Description = budget.Description,
            PeriodType = budget.PeriodType.ToString(),
            StartDate = periodStart,
            EndDate = periodEnd,
            IsRecurring = budget.IsRecurring,
            IsActive = budget.IsActive,
            CategoryCount = categoryIds.Count,
            TotalBudgeted = totalBudgeted,
            TotalSpent = totalSpent,
            TotalRemaining = totalRemaining,
            UsedPercentage = usedPercentage,
            DaysRemaining = budget.GetDaysRemaining(),
            IsCurrentPeriod = budget.ContainsDate(today)
        };
    }

    public decimal ProjectEndOfPeriodSpending(
        decimal currentSpent,
        int daysElapsed,
        int totalDays)
    {
        if (daysElapsed <= 0 || totalDays <= 0)
            return currentSpent;

        var dailyRate = currentSpent / daysElapsed;
        return Math.Round(dailyRate * totalDays, 2);
    }

    /// <summary>
    /// Gets all descendant category IDs for a given category
    /// </summary>
    private static HashSet<int> GetAllDescendantIds(int categoryId, List<Category> allCategories)
    {
        var result = new HashSet<int>();
        var queue = new Queue<int>();

        // Find direct children
        var directChildren = allCategories
            .Where(c => c.ParentCategoryId == categoryId && !c.IsDeleted)
            .Select(c => c.Id);

        foreach (var childId in directChildren)
        {
            queue.Enqueue(childId);
        }

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            result.Add(currentId);

            // Find children of current
            var children = allCategories
                .Where(c => c.ParentCategoryId == currentId && !c.IsDeleted)
                .Select(c => c.Id);

            foreach (var childId in children)
            {
                if (!result.Contains(childId))
                {
                    queue.Enqueue(childId);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Rounds a decimal to a friendly budget number
    /// </summary>
    private static decimal RoundToFriendlyNumber(decimal amount)
    {
        if (amount <= 0) return 0;

        // Round to nearest $5 for amounts under $100
        // Round to nearest $10 for amounts under $500
        // Round to nearest $25 for amounts under $1000
        // Round to nearest $50 for amounts $1000+

        if (amount < 100)
            return Math.Ceiling(amount / 5) * 5;
        if (amount < 500)
            return Math.Ceiling(amount / 10) * 10;
        if (amount < 1000)
            return Math.Ceiling(amount / 25) * 25;

        return Math.Ceiling(amount / 50) * 50;
    }
}
