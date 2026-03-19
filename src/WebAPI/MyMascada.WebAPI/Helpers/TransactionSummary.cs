namespace MyMascada.WebAPI.Helpers;

/// <summary>
/// Provides utility methods for summarizing and aggregating transaction data.
/// All methods are stateless and operate on lightweight input records
/// rather than domain entities, making them suitable for presentation-layer calculations.
/// </summary>
public static class TransactionSummary
{
    /// <summary>
    /// Represents a single transaction entry for summary calculations.
    /// </summary>
    /// <param name="Amount">The absolute amount of the transaction (always positive).</param>
    /// <param name="IsCredit">True if the transaction adds to the balance (income/credit); false for debits/expenses.</param>
    /// <param name="Category">Optional category name for grouping.</param>
    /// <param name="Date">The date the transaction occurred.</param>
    public sealed record TransactionEntry(
        decimal Amount,
        bool IsCredit,
        string? Category = null,
        DateTime? Date = null);

    /// <summary>
    /// Represents a per-category total in a category grouping result.
    /// </summary>
    /// <param name="Category">The category name (uncategorised transactions use an empty string).</param>
    /// <param name="Total">The net total for the category (credits positive, debits negative).</param>
    /// <param name="Count">The number of transactions in this category.</param>
    public sealed record CategoryTotal(string Category, decimal Total, int Count);

    /// <summary>
    /// Calculates a running balance from an ordered list of transactions,
    /// starting from an optional opening balance.
    /// </summary>
    /// <param name="transactions">
    /// The transactions in chronological order. If null or empty, returns an empty list.
    /// </param>
    /// <param name="openingBalance">The balance before the first transaction (defaults to 0).</param>
    /// <returns>
    /// A list of cumulative balances, one per transaction, where each element
    /// is the balance after applying the corresponding transaction.
    /// </returns>
    /// <example>
    /// <code>
    /// var entries = new[]
    /// {
    ///     new TransactionEntry(100m, IsCredit: true),
    ///     new TransactionEntry(30m,  IsCredit: false),
    /// };
    /// var balances = TransactionSummary.CalculateRunningBalance(entries, 500m);
    /// // balances: [600, 570]
    /// </code>
    /// </example>
    public static IReadOnlyList<decimal> CalculateRunningBalance(
        IEnumerable<TransactionEntry>? transactions,
        decimal openingBalance = 0m)
    {
        if (transactions is null)
            return Array.Empty<decimal>();

        var result = new List<decimal>();
        var balance = openingBalance;

        foreach (var tx in transactions)
        {
            var signed = tx.IsCredit ? Math.Abs(tx.Amount) : -Math.Abs(tx.Amount);
            balance += signed;
            result.Add(balance);
        }

        return result;
    }

    /// <summary>
    /// Groups transactions by their <see cref="TransactionEntry.Category"/>
    /// and returns the net total and count for each group.
    /// </summary>
    /// <param name="transactions">
    /// The transactions to group. If null or empty, returns an empty list.
    /// </param>
    /// <returns>
    /// A list of <see cref="CategoryTotal"/> ordered alphabetically by category name.
    /// Transactions with a null or whitespace category are grouped under an empty string.
    /// </returns>
    public static IReadOnlyList<CategoryTotal> GroupByCategory(
        IEnumerable<TransactionEntry>? transactions)
    {
        if (transactions is null)
            return Array.Empty<CategoryTotal>();

        return transactions
            .GroupBy(tx => string.IsNullOrWhiteSpace(tx.Category) ? string.Empty : tx.Category)
            .Select(g => new CategoryTotal(
                Category: g.Key,
                Total: g.Sum(tx => tx.IsCredit ? Math.Abs(tx.Amount) : -Math.Abs(tx.Amount)),
                Count: g.Count()))
            .OrderBy(c => c.Category, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Calculates the average daily spend (expenses only) across a date range.
    /// Only transactions whose <see cref="TransactionEntry.Date"/> falls within
    /// [<paramref name="from"/>, <paramref name="to"/>] and whose
    /// <see cref="TransactionEntry.IsCredit"/> is false are included.
    /// </summary>
    /// <param name="transactions">
    /// The transactions to analyse. If null or empty, returns 0.
    /// </param>
    /// <param name="from">Start of the date range (inclusive).</param>
    /// <param name="to">End of the date range (inclusive).</param>
    /// <returns>
    /// The average daily spend as a positive decimal, or 0 when there are no
    /// qualifying transactions or the date range is invalid.
    /// </returns>
    /// <remarks>
    /// The number of days is calculated as (<paramref name="to"/> - <paramref name="from"/>).Days + 1
    /// so that a single-day range counts as 1 day. If <paramref name="from"/> is after
    /// <paramref name="to"/>, the method returns 0.
    /// </remarks>
    public static decimal CalculateAverageDailySpend(
        IEnumerable<TransactionEntry>? transactions,
        DateTime from,
        DateTime to)
    {
        if (transactions is null || from > to)
            return 0m;

        var totalSpend = 0m;

        foreach (var tx in transactions)
        {
            if (tx.IsCredit || tx.Date is null)
                continue;

            var date = tx.Date.Value.Date;
            if (date >= from.Date && date <= to.Date)
                totalSpend += Math.Abs(tx.Amount);
        }

        if (totalSpend == 0m)
            return 0m;

        var days = (to.Date - from.Date).Days + 1;
        return totalSpend / days;
    }
}
