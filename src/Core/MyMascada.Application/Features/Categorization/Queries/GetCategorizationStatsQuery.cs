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

        // Use largest-remainder (Hamilton) apportionment so the four
        // per-method percentages always sum to exactly 100 when total > 0.
        // Rounding each share independently with Math.Round produces
        // cosmetic drift — three ~33% buckets would otherwise render as
        // "33% · 33% · 33%" (= 99) and confuse users scanning the card.
        var pcts = ApportionPercentages(new[] { byRules, byMl, byLlm, byBankCategory });

        return new CategorizationStatsResult
        {
            AutoCategorizedThisMonth = total,
            ProcessedByRules = byRules,
            ProcessedByML = byMl,
            ProcessedByLLM = byLlm,
            ProcessedByBankCategory = byBankCategory,
            RulesPercentage = pcts[0],
            MLPercentage = pcts[1],
            LLMPercentage = pcts[2],
            BankCategoryPercentage = pcts[3],
            NeedsReview = needsReview,
            PendingSuggestions = pendingSuggestions,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd
        };
    }

    /// <summary>
    /// Apportions 100 points across the input counts using the largest-
    /// remainder (Hamilton) method. The returned array preserves input
    /// order, every value is in [0, 100], and the sum equals exactly 100
    /// whenever the input total is greater than zero (all zeros in the
    /// input map to all zeros in the output). Ties on remainder are broken
    /// by the lower original index for stable output.
    /// </summary>
    private static int[] ApportionPercentages(int[] counts)
    {
        var n = counts.Length;
        var result = new int[n];
        var total = 0L;
        for (var i = 0; i < n; i++)
        {
            total += counts[i];
        }
        if (total == 0)
        {
            return result;
        }

        var remainders = new (int Index, double Remainder)[n];
        var floorSum = 0;
        for (var i = 0; i < n; i++)
        {
            var exact = counts[i] * 100.0 / total;
            var floor = (int)Math.Floor(exact);
            result[i] = floor;
            remainders[i] = (i, exact - floor);
            floorSum += floor;
        }

        var seatsRemaining = 100 - floorSum;
        if (seatsRemaining <= 0)
        {
            return result;
        }

        // Distribute the leftover points to the buckets with the largest
        // fractional remainders first. Stable tie-break on original index
        // keeps the output deterministic.
        var ordered = remainders
            .OrderByDescending(r => r.Remainder)
            .ThenBy(r => r.Index)
            .ToArray();
        for (var i = 0; i < seatsRemaining && i < n; i++)
        {
            result[ordered[i].Index]++;
        }
        return result;
    }
}
