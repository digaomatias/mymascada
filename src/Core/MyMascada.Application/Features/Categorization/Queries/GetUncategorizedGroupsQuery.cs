using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categorization.Services;

namespace MyMascada.Application.Features.Categorization.Queries;

/// <summary>
/// Returns uncategorized transactions grouped by normalized description,
/// ordered by frequency (most-common groups first). Used by the
/// quick-categorize wizard to let users "teach by example" — categorizing
/// a single group applies the category to all transactions inside it.
/// </summary>
public class GetUncategorizedGroupsQuery : IRequest<UncategorizedGroupsResult>
{
    public Guid UserId { get; set; }

    /// <summary>
    /// Maximum number of groups to return. Defaults to 20 (enough for a
    /// wizard session without overwhelming the user).
    /// </summary>
    public int MaxGroups { get; set; } = 20;

    /// <summary>
    /// Minimum number of transactions a group must contain to be included.
    /// Defaults to 1 — every group gets surfaced, but the frontend can
    /// raise this to hide one-off transactions.
    /// </summary>
    public int MinGroupSize { get; set; } = 1;
}

public class UncategorizedGroupsResult
{
    /// <summary>
    /// The groups returned to the frontend, capped at
    /// <see cref="GetUncategorizedGroupsQuery.MaxGroups"/> and filtered to
    /// groups with at least <see cref="GetUncategorizedGroupsQuery.MinGroupSize"/>
    /// members.
    /// </summary>
    public List<UncategorizedGroupDto> Groups { get; set; } = new();

    /// <summary>
    /// Total count of uncategorized transactions for the user, straight from
    /// <c>CountUncategorizedTransactionsAsync</c>. This is the ground-truth
    /// "needs review" figure surfaced in the dashboard badge.
    ///
    /// <b>IMPORTANT:</b> this value commonly diverges from
    /// <see cref="GroupedTransactions"/> — they answer different questions:
    /// <list type="bullet">
    ///   <item><description><c>TotalUncategorized</c>: how many rows need review in total.</description></item>
    ///   <item><description><c>GroupedTransactions</c>: how many rows ended up in the groups we're returning *right now* (subject to <c>MinGroupSize</c> and <c>MaxGroups</c>).</description></item>
    /// </list>
    /// Singleton-merchant rows (filtered out by <c>MinGroupSize</c>) and rows
    /// beyond the top <c>MaxGroups</c> cluster are counted by
    /// <c>TotalUncategorized</c> but not by <c>GroupedTransactions</c>, so
    /// <c>TotalUncategorized &gt;= GroupedTransactions</c> is the normal
    /// relationship.
    /// </summary>
    public int TotalUncategorized { get; set; }

    /// <summary>
    /// Sum of <see cref="UncategorizedGroupDto.TransactionCount"/> across the
    /// returned <see cref="Groups"/>. See <see cref="TotalUncategorized"/>
    /// for the ground-truth "rows needing review" count and why these two
    /// values routinely differ.
    /// </summary>
    public int GroupedTransactions { get; set; }
}

public class UncategorizedGroupDto
{
    /// <summary>
    /// Normalized description used as the group key. Stable identifier
    /// for the frontend to send back in bulk-categorize requests.
    /// </summary>
    public string NormalizedDescription { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable sample description (first transaction in the group).
    /// </summary>
    public string SampleDescription { get; set; } = string.Empty;

    /// <summary>
    /// Total number of uncategorized transactions in this group.
    /// </summary>
    public int TransactionCount { get; set; }

    /// <summary>
    /// Sum of transaction amounts (signed — negative for expense groups).
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Transaction IDs in the group. Sent back verbatim in bulk-categorize
    /// so the backend does not have to re-query.
    /// </summary>
    public List<int> TransactionIds { get; set; } = new();

    /// <summary>
    /// Up to 3 sample transactions for the UI preview.
    /// </summary>
    public List<UncategorizedGroupSample> Samples { get; set; } = new();
}

public class UncategorizedGroupSample
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string AccountName { get; set; } = string.Empty;
}

public class GetUncategorizedGroupsQueryHandler
    : IRequestHandler<GetUncategorizedGroupsQuery, UncategorizedGroupsResult>
{
    private readonly ITransactionRepository _transactionRepository;

    public GetUncategorizedGroupsQueryHandler(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    public async Task<UncategorizedGroupsResult> Handle(
        GetUncategorizedGroupsQuery request,
        CancellationToken cancellationToken)
    {
        // CLAUDE.md "Database-Level Filtering" exception: grouping and
        // normalization must happen in memory because
        // `DescriptionNormalizer.Normalize` is a C# method that strips
        // dates, reference numbers, and special chars using regex passes
        // EF Core cannot translate into SQL. A DB-level equivalent would
        // require either a persisted normalized_description column (costly
        // migration + write-path rewrite across every transaction
        // insert/update) or a stored procedure duplicating the normalizer
        // logic (fragile and easy to drift out of sync with the C# version).
        //
        // Mitigations: the fetch is capped at 1000 rows (`maxCount`), which
        // bounds memory use to ~100KB of Transaction rows for the largest
        // realistic user — the same budget we already accept in the
        // categorization pipeline and SimilarityMatchingService. The wizard
        // caps UI display server-side via `request.MaxGroups`, not by
        // over-fetching, so returning more transactions here never inflates
        // the response size.
        var transactions = (await _transactionRepository.GetUncategorizedTransactionsAsync(
            request.UserId, maxCount: 1000, cancellationToken)).ToList();

        var totalUncategorized = await _transactionRepository.CountUncategorizedTransactionsAsync(
            request.UserId, cancellationToken);

        if (transactions.Count == 0)
        {
            return new UncategorizedGroupsResult
            {
                Groups = new(),
                TotalUncategorized = totalUncategorized,
                GroupedTransactions = 0
            };
        }

        var groups = transactions
            .Select(t => new { Transaction = t, Normalized = DescriptionNormalizer.Normalize(t.Description) })
            .Where(x => !string.IsNullOrWhiteSpace(x.Normalized))
            .GroupBy(x => x.Normalized)
            .Where(g => g.Count() >= request.MinGroupSize)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Sum(x => Math.Abs(x.Transaction.Amount)))
            .Take(request.MaxGroups)
            .Select(g =>
            {
                var items = g.Select(x => x.Transaction).ToList();
                return new UncategorizedGroupDto
                {
                    NormalizedDescription = g.Key,
                    SampleDescription = items[0].Description,
                    TransactionCount = items.Count,
                    TotalAmount = items.Sum(t => t.Amount),
                    TransactionIds = items.Select(t => t.Id).ToList(),
                    Samples = items
                        .Take(3)
                        .Select(t => new UncategorizedGroupSample
                        {
                            Id = t.Id,
                            Description = t.Description,
                            Amount = t.Amount,
                            TransactionDate = t.TransactionDate,
                            AccountName = t.Account?.Name ?? string.Empty
                        })
                        .ToList()
                };
            })
            .ToList();

        return new UncategorizedGroupsResult
        {
            Groups = groups,
            TotalUncategorized = totalUncategorized,
            GroupedTransactions = groups.Sum(g => g.TransactionCount)
        };
    }
}
