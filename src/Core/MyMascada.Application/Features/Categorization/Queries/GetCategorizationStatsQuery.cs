using MediatR;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.Categorization.Queries;

/// <summary>
/// Monthly categorization metrics for the dashboard stats card — auto-applied
/// counts broken down by method, how many transactions need review, and how
/// many rule suggestions are waiting for the user.
/// </summary>
public class GetCategorizationStatsQuery : IRequest<CategorizationStatsResult>
{
    public Guid UserId { get; set; }
}

public class CategorizationStatsResult
{
    /// <summary>
    /// Total transactions auto-categorized in the current calendar month.
    /// </summary>
    public int AutoCategorizedThisMonth { get; set; }

    /// <summary>
    /// Auto-categorized this month by the Rules handler.
    /// </summary>
    public int ProcessedByRules { get; set; }

    /// <summary>
    /// Auto-categorized this month by the ML (similarity-matching) handler.
    /// </summary>
    public int ProcessedByML { get; set; }

    /// <summary>
    /// Auto-categorized this month by the LLM handler.
    /// </summary>
    public int ProcessedByLLM { get; set; }

    /// <summary>
    /// Auto-categorized this month by the BankCategory handler (mapped from
    /// the upstream bank provider's category, e.g. Akahu).
    /// </summary>
    public int ProcessedByBankCategory { get; set; }

    /// <summary>
    /// Percentage processed by Rules (0-100).
    /// </summary>
    public int RulesPercentage { get; set; }

    /// <summary>
    /// Percentage processed by ML (0-100).
    /// </summary>
    public int MLPercentage { get; set; }

    /// <summary>
    /// Percentage processed by LLM (0-100).
    /// </summary>
    public int LLMPercentage { get; set; }

    /// <summary>
    /// Percentage processed by BankCategory (0-100).
    /// </summary>
    public int BankCategoryPercentage { get; set; }

    /// <summary>
    /// Total transactions currently uncategorized / awaiting review.
    /// </summary>
    public int NeedsReview { get; set; }

    /// <summary>
    /// Pending rule suggestions the user has not reviewed yet.
    /// </summary>
    public int PendingSuggestions { get; set; }

    /// <summary>
    /// Start of the stats window (first day of current month, UTC).
    /// </summary>
    public DateTime PeriodStart { get; set; }

    /// <summary>
    /// End of the stats window (exclusive — first day of next month, UTC).
    /// </summary>
    public DateTime PeriodEnd { get; set; }
}

public class GetCategorizationStatsQueryHandler
    : IRequestHandler<GetCategorizationStatsQuery, CategorizationStatsResult>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IRuleSuggestionRepository _ruleSuggestionRepository;

    public GetCategorizationStatsQueryHandler(
        ITransactionRepository transactionRepository,
        IRuleSuggestionRepository ruleSuggestionRepository)
    {
        _transactionRepository = transactionRepository;
        _ruleSuggestionRepository = ruleSuggestionRepository;
    }

    public async Task<CategorizationStatsResult> Handle(
        GetCategorizationStatsQuery request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var periodStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1);

        var countsByMethod = await _transactionRepository.GetAutoCategorizationCountsByMethodAsync(
            request.UserId, periodStart, periodEnd, cancellationToken);

        var byRules = countsByMethod.GetValueOrDefault("Rule", 0)
                      + countsByMethod.GetValueOrDefault("Rules", 0);
        var byMl = countsByMethod.GetValueOrDefault("ML", 0);
        var byLlm = countsByMethod.GetValueOrDefault("LLM", 0);
        // BankCategoryHandler tags its applied transactions with HandlerType
        // "BankCategory". Without this branch, bank-mapped auto-categorizations
        // were silently dropped from the monthly totals + percentages.
        var byBankCategory = countsByMethod.GetValueOrDefault("BankCategory", 0);
        var total = byRules + byMl + byLlm + byBankCategory;

        var needsReview = await _transactionRepository.CountUncategorizedTransactionsAsync(
            request.UserId, cancellationToken);

        var pendingSuggestions = await _ruleSuggestionRepository.CountPendingSuggestionsAsync(
            request.UserId, cancellationToken);

        return new CategorizationStatsResult
        {
            AutoCategorizedThisMonth = total,
            ProcessedByRules = byRules,
            ProcessedByML = byMl,
            ProcessedByLLM = byLlm,
            ProcessedByBankCategory = byBankCategory,
            RulesPercentage = total > 0 ? (int)Math.Round(byRules * 100.0 / total) : 0,
            MLPercentage = total > 0 ? (int)Math.Round(byMl * 100.0 / total) : 0,
            LLMPercentage = total > 0 ? (int)Math.Round(byLlm * 100.0 / total) : 0,
            BankCategoryPercentage = total > 0 ? (int)Math.Round(byBankCategory * 100.0 / total) : 0,
            NeedsReview = needsReview,
            PendingSuggestions = pendingSuggestions,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd
        };
    }
}
