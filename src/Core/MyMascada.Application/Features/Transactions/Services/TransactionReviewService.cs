using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Transactions.Services;

/// <summary>
/// Service for determining if transactions need review based on smart flagging criteria
/// </summary>
public class TransactionReviewService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountRepository _accountRepository;

    public TransactionReviewService(
        ITransactionRepository transactionRepository,
        IAccountRepository accountRepository)
    {
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
    }

    /// <summary>
    /// Determines if a transaction should be flagged for review
    /// </summary>
    public async Task<bool> ShouldFlagForReview(Transaction transaction, Guid userId)
    {
        // Manual transactions are automatically reviewed (user created them)
        if (transaction.Source == TransactionSource.Manual)
            return false;

        // Check various flagging criteria
        var flags = new List<string>();

        // 1. Large transactions (significantly above average)
        if (await IsLargeTransaction(transaction, userId))
            flags.Add("Large amount");

        // 2. Unusual vendor/description
        if (await IsUnusualVendor(transaction, userId))
            flags.Add("New vendor");

        // 3. Missing categorization
        if (!transaction.CategoryId.HasValue)
            flags.Add("No category");

        // 4. Potential duplicate
        if (await IsPotentialDuplicate(transaction, userId))
            flags.Add("Potential duplicate");

        // 5. Weekend spending (if user doesn't usually spend on weekends)
        if (await IsUnusualWeekendSpending(transaction, userId))
            flags.Add("Weekend spending");

        // 6. Round amounts (often manual/unusual)
        if (IsRoundAmount(transaction.Amount))
            flags.Add("Round amount");

        // Flag if any criteria are met
        return flags.Count > 0;
    }

    /// <summary>
    /// Gets the reasons why a transaction was flagged
    /// </summary>
    public async Task<List<string>> GetFlaggingReasons(Transaction transaction, Guid userId)
    {
        var reasons = new List<string>();

        if (transaction.Source == TransactionSource.Manual)
            return reasons;

        if (await IsLargeTransaction(transaction, userId))
            reasons.Add("Amount is significantly higher than usual");

        if (await IsUnusualVendor(transaction, userId))
            reasons.Add("First time transaction with this vendor");

        if (!transaction.CategoryId.HasValue)
            reasons.Add("Transaction needs categorization");

        if (await IsPotentialDuplicate(transaction, userId))
            reasons.Add("Similar transaction found recently");

        if (await IsUnusualWeekendSpending(transaction, userId))
            reasons.Add("Unusual weekend spending pattern");

        if (IsRoundAmount(transaction.Amount))
            reasons.Add("Round amount may need verification");

        return reasons;
    }

    /// <summary>
    /// Checks if transaction amount is significantly larger than user's average
    /// </summary>
    private async Task<bool> IsLargeTransaction(Transaction transaction, Guid userId)
    {
        try
        {
            var recentTransactions = await _transactionRepository.GetRecentAsync(userId, 50);
            if (!recentTransactions.Any()) return false;

            var amounts = recentTransactions
                .Where(t => t.Id != transaction.Id) // Exclude current transaction
                .Select(t => Math.Abs(t.Amount))
                .ToList();

            if (amounts.Count < 5) return false; // Need some history

            var average = amounts.Average();
            var stdDev = CalculateStandardDeviation(amounts);
            var threshold = average + (2 * (decimal)stdDev); // 2 standard deviations above average

            return Math.Abs(transaction.Amount) > threshold && Math.Abs(transaction.Amount) > 100; // Also require minimum amount
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if this is the first transaction with this vendor/description pattern
    /// </summary>
    private async Task<bool> IsUnusualVendor(Transaction transaction, Guid userId)
    {
        try
        {
            // Get recent transactions to check for similar descriptions
            var recentTransactions = await _transactionRepository.GetRecentAsync(userId, 100);
            
            // Extract potential vendor name (first few words)
            var words = transaction.Description.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return false;

            var vendorPattern = words.Take(2).FirstOrDefault()?.ToUpperInvariant();
            if (string.IsNullOrEmpty(vendorPattern) || vendorPattern.Length < 3) return false;

            // Check if we've seen this vendor before
            var similarTransactions = recentTransactions
                .Where(t => t.Id != transaction.Id && 
                           t.Description.ToUpperInvariant().Contains(vendorPattern))
                .Any();

            return !similarTransactions;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks for potential duplicate transactions
    /// </summary>
    private async Task<bool> IsPotentialDuplicate(Transaction transaction, Guid userId)
    {
        try
        {
            var duplicateWindow = TimeSpan.FromDays(3); // Check within 3 days
            var duplicate = await _transactionRepository.GetRecentDuplicateAsync(
                transaction.AccountId,
                transaction.Amount,
                transaction.Description,
                duplicateWindow);

            return duplicate != null && duplicate.Id != transaction.Id;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if this is unusual weekend spending for the user
    /// </summary>
    private async Task<bool> IsUnusualWeekendSpending(Transaction transaction, Guid userId)
    {
        try
        {
            // Only flag if it's a weekend
            var dayOfWeek = transaction.TransactionDate.DayOfWeek;
            if (dayOfWeek != DayOfWeek.Saturday && dayOfWeek != DayOfWeek.Sunday)
                return false;

            // Check user's weekend spending pattern
            var recentTransactions = await _transactionRepository.GetRecentAsync(userId, 100);
            var weekendTransactions = recentTransactions
                .Where(t => t.TransactionDate.DayOfWeek == DayOfWeek.Saturday || 
                           t.TransactionDate.DayOfWeek == DayOfWeek.Sunday)
                .Count();

            var totalTransactions = recentTransactions.Count();
            if (totalTransactions < 20) return false; // Need enough history

            var weekendPercentage = (double)weekendTransactions / totalTransactions;
            
            // Flag if user typically doesn't spend on weekends (less than 10% of transactions)
            return weekendPercentage < 0.1 && Math.Abs(transaction.Amount) > 50;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if amount is a round number (potentially manual/suspicious)
    /// </summary>
    private bool IsRoundAmount(decimal amount)
    {
        var absAmount = Math.Abs(amount);
        
        // Round amounts that might be suspicious
        return absAmount % 1 == 0 && // Whole number
               (absAmount % 10 == 0 || absAmount % 25 == 0 || absAmount % 50 == 0 || absAmount % 100 == 0) &&
               absAmount >= 100; // Only flag larger round amounts
    }

    /// <summary>
    /// Calculates standard deviation for a list of numbers
    /// </summary>
    private double CalculateStandardDeviation(IEnumerable<decimal> values)
    {
        var avg = values.Average();
        var squaredDifferences = values.Select(v => Math.Pow((double)(v - avg), 2));
        return Math.Sqrt(squaredDifferences.Average());
    }
}