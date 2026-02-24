using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Goals.Queries;

// --- Query ---
public record GetEmergencyFundAnalysisQuery(Guid UserId, int GoalId, bool IncludeLlmAnalysis = false) : IRequest<EmergencyFundAnalysisDto?>;

// --- DTOs ---
public record EmergencyFundAnalysisDto
{
    public decimal AverageMonthlyExpenses { get; init; }
    public decimal AverageMonthlyExpenses6M { get; init; }
    public decimal OnboardingMonthlyExpenses { get; init; }
    public decimal RecommendedTarget3M { get; init; }
    public decimal RecommendedTarget6M { get; init; }
    public decimal CurrentAmount { get; init; }
    public decimal MonthsCovered { get; init; }
    public int TransactionMonthsAvailable { get; init; }
    public List<MonthlyExpenseBreakdown> MonthlyBreakdown { get; init; } = [];
    public decimal MonthlyRecurringTotal { get; init; }
    public int ActiveRecurringCount { get; init; }
    public EssentialExpenseAnalysis? EssentialAnalysis { get; init; }
}

public record MonthlyExpenseBreakdown
{
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal TotalExpenses { get; init; }
    public decimal Income { get; init; }
}

public record EssentialExpenseAnalysis
{
    public decimal EstimatedMonthlyEssentials { get; init; }
    public decimal EstimatedMonthlyDiscretionary { get; init; }
    public decimal RecommendedTarget3M { get; init; }
    public decimal RecommendedTarget6M { get; init; }
    public List<ExpenseCategoryBreakdown> Categories { get; init; } = [];
    public string Reasoning { get; init; } = "";
}

public record ExpenseCategoryBreakdown
{
    public string CategoryName { get; init; } = "";
    public decimal MonthlyAverage { get; init; }
    public bool IsEssential { get; init; }
}

// --- Handler ---
public class GetEmergencyFundAnalysisHandler : IRequestHandler<GetEmergencyFundAnalysisQuery, EmergencyFundAnalysisDto?>
{
    private readonly IGoalRepository _goalRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IRecurringPatternRepository _recurringPatternRepository;
    private readonly IUserFinancialProfileRepository _userFinancialProfileRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUserAiKernelFactory _kernelFactory;
    private readonly ILogger<GetEmergencyFundAnalysisHandler> _logger;

    public GetEmergencyFundAnalysisHandler(
        IGoalRepository goalRepository,
        ITransactionRepository transactionRepository,
        IRecurringPatternRepository recurringPatternRepository,
        IUserFinancialProfileRepository userFinancialProfileRepository,
        ICategoryRepository categoryRepository,
        IUserAiKernelFactory kernelFactory,
        ILogger<GetEmergencyFundAnalysisHandler> logger)
    {
        _goalRepository = goalRepository;
        _transactionRepository = transactionRepository;
        _recurringPatternRepository = recurringPatternRepository;
        _userFinancialProfileRepository = userFinancialProfileRepository;
        _categoryRepository = categoryRepository;
        _kernelFactory = kernelFactory;
        _logger = logger;
    }

    public async Task<EmergencyFundAnalysisDto?> Handle(GetEmergencyFundAnalysisQuery request, CancellationToken cancellationToken)
    {
        // 1. Validate goal exists, belongs to user, and is EmergencyFund type
        var goal = await _goalRepository.GetGoalByIdAsync(request.GoalId, request.UserId, cancellationToken);
        if (goal == null || goal.GoalType != GoalType.EmergencyFund)
        {
            return null;
        }

        // 2. Basic calculation
        var now = DateTimeProvider.UtcNow;
        var sixMonthsAgo = now.AddMonths(-6);
        var threeMonthsAgo = now.AddMonths(-3);

        // Fetch transactions for last 6 months
        var transactions = (await _transactionRepository.GetByDateRangeAsync(request.UserId, sixMonthsAgo, now)).ToList();

        // Filter to expenses only: Amount < 0, not transfers, not excluded
        var expenseTransactions = transactions
            .Where(t => t.Amount < 0 && !t.IsTransfer() && !t.IsExcluded)
            .ToList();

        // Group by year/month
        var monthlyGroups = transactions
            .Where(t => !t.IsTransfer() && !t.IsExcluded)
            .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
            .Select(g => new MonthlyExpenseBreakdown
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                TotalExpenses = Math.Abs(g.Where(t => t.Amount < 0).Sum(t => t.Amount)),
                Income = g.Where(t => t.Amount > 0).Sum(t => t.Amount)
            })
            .OrderByDescending(m => m.Year)
            .ThenByDescending(m => m.Month)
            .ToList();

        var transactionMonthsAvailable = monthlyGroups.Count;

        // Calculate 3-month average (only months within the last 3 months)
        var threeMonthExpenses = expenseTransactions
            .Where(t => t.TransactionDate >= threeMonthsAgo)
            .ToList();

        var threeMonthGroups = threeMonthExpenses
            .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
            .ToList();

        var threeMonthCount = threeMonthGroups.Count;
        var threeMonthTotal = Math.Abs(threeMonthExpenses.Sum(t => t.Amount));
        var avgMonthlyExpenses3M = threeMonthCount > 0
            ? Math.Round(threeMonthTotal / threeMonthCount, 2)
            : 0m;

        // Calculate 6-month average
        var sixMonthGroups = expenseTransactions
            .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
            .ToList();

        var sixMonthCount = sixMonthGroups.Count;
        var sixMonthTotal = Math.Abs(expenseTransactions.Sum(t => t.Amount));
        var avgMonthlyExpenses6M = sixMonthCount > 0
            ? Math.Round(sixMonthTotal / sixMonthCount, 2)
            : 0m;

        // Fetch recurring patterns for monthly recurring total
        var activePatterns = (await _recurringPatternRepository.GetActiveAsync(request.UserId, cancellationToken)).ToList();
        var monthlyRecurringTotal = activePatterns.Sum(p => p.GetMonthlyCost());
        var activeRecurringCount = activePatterns.Count;

        // Determine current amount: live balance if linked account, otherwise stored
        decimal currentAmount;
        if (goal.LinkedAccountId.HasValue)
        {
            var accountBalances = await _transactionRepository.GetAccountBalancesAsync(request.UserId);
            currentAmount = accountBalances.GetValueOrDefault(goal.LinkedAccountId.Value, 0m);
        }
        else
        {
            currentAmount = goal.CurrentAmount;
        }

        // Fetch UserFinancialProfile for onboarding expenses
        var profile = await _userFinancialProfileRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        var onboardingMonthlyExpenses = profile?.MonthlyExpenses ?? 0m;

        // Determine the effective average for MonthsCovered calculation
        // Use 3-month avg; if < 1 month of data, fall back to onboarding expenses
        var effectiveAvg = threeMonthCount >= 1 ? avgMonthlyExpenses3M : onboardingMonthlyExpenses;
        var monthsCovered = effectiveAvg > 0
            ? Math.Round(currentAmount / effectiveAvg, 2)
            : 0m;

        // Recommended targets — use best available average, falling back through the chain
        var effectiveAvgForTargets = avgMonthlyExpenses3M > 0 ? avgMonthlyExpenses3M
            : avgMonthlyExpenses6M > 0 ? avgMonthlyExpenses6M
            : onboardingMonthlyExpenses;
        var recommendedTarget3M = Math.Round(effectiveAvgForTargets * 3, 2);
        var recommendedTarget6M = Math.Round(effectiveAvgForTargets * 6, 2);

        // 3. LLM-enhanced essential expense analysis (only if requested)
        EssentialExpenseAnalysis? essentialAnalysis = null;
        if (request.IncludeLlmAnalysis)
        {
            essentialAnalysis = await BuildEssentialAnalysisAsync(request.UserId, expenseTransactions, threeMonthsAgo, cancellationToken);
        }

        return new EmergencyFundAnalysisDto
        {
            AverageMonthlyExpenses = avgMonthlyExpenses3M,
            AverageMonthlyExpenses6M = avgMonthlyExpenses6M,
            OnboardingMonthlyExpenses = onboardingMonthlyExpenses,
            RecommendedTarget3M = recommendedTarget3M,
            RecommendedTarget6M = recommendedTarget6M,
            CurrentAmount = currentAmount,
            MonthsCovered = monthsCovered,
            TransactionMonthsAvailable = transactionMonthsAvailable,
            MonthlyBreakdown = monthlyGroups,
            MonthlyRecurringTotal = monthlyRecurringTotal,
            ActiveRecurringCount = activeRecurringCount,
            EssentialAnalysis = essentialAnalysis
        };
    }

    private async Task<EssentialExpenseAnalysis?> BuildEssentialAnalysisAsync(
        Guid userId,
        List<Domain.Entities.Transaction> expenseTransactions,
        DateTime threeMonthsAgo,
        CancellationToken cancellationToken)
    {
        try
        {
            var kernel = await _kernelFactory.CreateKernelForUserAsync(userId);
            if (kernel == null)
            {
                _logger.LogInformation("AI kernel not available for user {UserId}, skipping essential expense analysis", userId);
                return null;
            }

            // Build category breakdown from the 3-month window
            var categories = (await _categoryRepository.GetByUserIdAsync(userId)).ToDictionary(c => c.Id, c => c.Name);

            var threeMonthExpenses = expenseTransactions
                .Where(t => t.TransactionDate >= threeMonthsAgo)
                .ToList();

            var threeMonthDistinctMonths = threeMonthExpenses
                .Select(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
                .Distinct()
                .Count();

            if (threeMonthDistinctMonths == 0)
            {
                _logger.LogInformation("No expense data in 3-month window for user {UserId}, skipping LLM analysis", userId);
                return null;
            }

            var categoryBreakdown = threeMonthExpenses
                .GroupBy(t => t.CategoryId)
                .Select(g =>
                {
                    var categoryName = g.Key.HasValue && categories.TryGetValue(g.Key.Value, out var name)
                        ? name
                        : "Uncategorized";
                    var totalAmount = Math.Abs(g.Sum(t => t.Amount));
                    var monthlyAverage = Math.Round(totalAmount / threeMonthDistinctMonths, 2);
                    return new { CategoryName = categoryName, MonthlyAverage = monthlyAverage };
                })
                .OrderByDescending(c => c.MonthlyAverage)
                .ToList();

            // Build LLM prompt
            var categoryLines = string.Join("\n", categoryBreakdown.Select(c => $"- {c.CategoryName}: ${c.MonthlyAverage:F2}/month"));

            var systemMessage = """
                You are a financial analyst. Classify expense categories as essential or discretionary.
                Essential expenses are those required for basic living: housing, utilities, groceries, transportation, insurance, healthcare, debt payments.
                Discretionary expenses are optional: dining out, entertainment, subscriptions, shopping, hobbies, travel.

                Respond ONLY with valid JSON in this exact format (no markdown, no code fences):
                {
                  "categories": [
                    { "name": "Category Name", "monthlyAverage": 123.45, "isEssential": true }
                  ],
                  "reasoning": "Brief explanation of classification decisions"
                }
                """;

            var userMessage = $"""
                Classify these expense categories as essential or discretionary based on their names:

                {categoryLines}

                Return the JSON response with each category classified.
                """;

            var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemMessage);
            chatHistory.AddUserMessage(userMessage);

            var response = await chatCompletionService.GetChatMessageContentAsync(chatHistory, cancellationToken: cancellationToken);
            var responseContent = response.Content;

            if (string.IsNullOrWhiteSpace(responseContent))
            {
                _logger.LogWarning("Empty LLM response for essential expense analysis, user {UserId}", userId);
                return null;
            }

            // Parse JSON response
            var parsed = ParseLlmResponse(responseContent, categoryBreakdown.Select(c => new ExpenseCategoryBreakdown
            {
                CategoryName = c.CategoryName,
                MonthlyAverage = c.MonthlyAverage,
                IsEssential = false
            }).ToList());

            if (parsed == null)
            {
                _logger.LogWarning("Failed to parse LLM response for essential expense analysis, user {UserId}", userId);
                return null;
            }

            var essentials = parsed.Categories.Where(c => c.IsEssential).Sum(c => c.MonthlyAverage);
            var discretionary = parsed.Categories.Where(c => !c.IsEssential).Sum(c => c.MonthlyAverage);

            return new EssentialExpenseAnalysis
            {
                EstimatedMonthlyEssentials = Math.Round(essentials, 2),
                EstimatedMonthlyDiscretionary = Math.Round(discretionary, 2),
                RecommendedTarget3M = Math.Round(essentials * 3, 2),
                RecommendedTarget6M = Math.Round(essentials * 6, 2),
                Categories = parsed.Categories,
                Reasoning = parsed.Reasoning
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing LLM essential expense analysis for user {UserId}", userId);
            return null;
        }
    }

    private LlmClassificationResult? ParseLlmResponse(string responseContent, List<ExpenseCategoryBreakdown> fallbackCategories)
    {
        try
        {
            // Strip markdown code fences if present
            var jsonContent = responseContent.Trim();
            if (jsonContent.StartsWith("```"))
            {
                var firstNewline = jsonContent.IndexOf('\n');
                if (firstNewline >= 0)
                {
                    jsonContent = jsonContent[(firstNewline + 1)..];
                }
                if (jsonContent.EndsWith("```"))
                {
                    jsonContent = jsonContent[..^3].Trim();
                }
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var result = JsonSerializer.Deserialize<LlmClassificationResponse>(jsonContent, options);
            if (result?.Categories == null || result.Categories.Count == 0)
            {
                return null;
            }

            // Use actual calculated amounts from our data, not LLM-provided amounts (which may hallucinate)
            var originalAmounts = fallbackCategories.ToDictionary(
                c => c.CategoryName, c => c.MonthlyAverage, StringComparer.OrdinalIgnoreCase);

            var categories = result.Categories.Select(c => new ExpenseCategoryBreakdown
            {
                CategoryName = c.Name ?? "",
                MonthlyAverage = originalAmounts.GetValueOrDefault(c.Name ?? "", c.MonthlyAverage),
                IsEssential = c.IsEssential
            }).ToList();

            return new LlmClassificationResult
            {
                Categories = categories,
                Reasoning = result.Reasoning ?? ""
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize LLM classification response");
            return null;
        }
    }

    // Internal DTOs for JSON deserialization of LLM response
    private class LlmClassificationResponse
    {
        public List<LlmCategoryEntry> Categories { get; set; } = [];
        public string? Reasoning { get; set; }
    }

    private class LlmCategoryEntry
    {
        public string? Name { get; set; }
        public decimal MonthlyAverage { get; set; }
        public bool IsEssential { get; set; }
    }

    private class LlmClassificationResult
    {
        public List<ExpenseCategoryBreakdown> Categories { get; set; } = [];
        public string Reasoning { get; set; } = "";
    }
}
