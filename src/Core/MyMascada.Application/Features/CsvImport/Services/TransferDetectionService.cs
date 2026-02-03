using MyMascada.Application.Features.CsvImport.DTOs;
using MyMascada.Application.Features.Transactions.DTOs;
using MyMascada.Domain.Enums;
using System.Diagnostics;

namespace MyMascada.Application.Features.CsvImport.Services;

/// <summary>
/// Service for detecting potential transfers between accounts during CSV import
/// </summary>
public class TransferDetectionService
{
    private readonly TransferDetectionConfig _config;

    public TransferDetectionService(TransferDetectionConfig? config = null)
    {
        _config = config ?? new TransferDetectionConfig();
    }

    /// <summary>
    /// Analyzes a list of transactions to find potential transfers
    /// </summary>
    public TransferDetectionResult DetectPotentialTransfers(List<TransactionDto> transactions)
    {
        var stopwatch = Stopwatch.StartNew();
        var candidates = new List<TransferCandidate>();

        // Group transactions by date (with tolerance)
        var transactionGroups = GroupTransactionsByDate(transactions);

        foreach (var group in transactionGroups)
        {
            var groupCandidates = FindTransfersInGroup(group.Value);
            candidates.AddRange(groupCandidates);
        }

        // Filter by minimum confidence score
        var filteredCandidates = candidates
            .Where(c => c.ConfidenceScore >= _config.MinimumConfidenceScore)
            .OrderByDescending(c => c.ConfidenceScore)
            .ToList();

        stopwatch.Stop();

        return new TransferDetectionResult
        {
            Candidates = filteredCandidates,
            TransactionsAnalyzed = transactions.Count,
            CandidatesFound = filteredCandidates.Count,
            ProcessingTimeMs = stopwatch.ElapsedMilliseconds
        };
    }

    /// <summary>
    /// Groups transactions by date with tolerance for processing delays
    /// </summary>
    private Dictionary<DateTime, List<TransactionDto>> GroupTransactionsByDate(List<TransactionDto> transactions)
    {
        var groups = new Dictionary<DateTime, List<TransactionDto>>();

        foreach (var transaction in transactions)
        {
            var dateKey = transaction.TransactionDate.Date;
            
            // Look for existing group within tolerance
            var existingKey = groups.Keys.FirstOrDefault(k => 
                Math.Abs((k - dateKey).TotalDays) <= _config.MaxDaysDifference);

            if (existingKey != default)
            {
                groups[existingKey].Add(transaction);
            }
            else
            {
                groups[dateKey] = new List<TransactionDto> { transaction };
            }
        }

        return groups;
    }

    /// <summary>
    /// Finds potential transfers within a group of transactions from similar dates
    /// </summary>
    private List<TransferCandidate> FindTransfersInGroup(List<TransactionDto> transactions)
    {
        var candidates = new List<TransferCandidate>();

        // Separate debits and credits
        var debits = transactions.Where(t => t.Amount < 0).ToList();
        var credits = transactions.Where(t => t.Amount > 0).ToList();

        // Match debits with credits
        foreach (var debit in debits)
        {
            foreach (var credit in credits)
            {
                // Skip if same account (can't transfer to self)
                if (debit.AccountId == credit.AccountId)
                    continue;

                // Skip if already part of a transfer
                if (debit.Type == TransactionType.TransferComponent || 
                    credit.Type == TransactionType.TransferComponent)
                    continue;

                var candidate = EvaluateTransferCandidate(debit, credit);
                if (candidate != null)
                {
                    candidates.Add(candidate);
                }
            }
        }

        // Remove duplicate candidates (same pair matched multiple ways)
        return RemoveDuplicateCandidates(candidates);
    }

    /// <summary>
    /// Evaluates if two transactions could be a transfer and calculates confidence score
    /// </summary>
    private TransferCandidate? EvaluateTransferCandidate(TransactionDto debit, TransactionDto credit)
    {
        var criteria = new List<string>();
        var confidence = 0.0m;

        // Amount matching (most important factor)
        var debitAmount = Math.Abs(debit.Amount);
        var creditAmount = Math.Abs(credit.Amount);
        var amountDifference = Math.Abs(debitAmount - creditAmount);
        var amountDifferencePercent = amountDifference / Math.Max(debitAmount, creditAmount);

        if (amountDifferencePercent <= _config.MaxAmountDifferencePercent)
        {
            confidence += 0.4m; // 40% for exact or near-exact amount match
            criteria.Add($"Amount match within {amountDifferencePercent:P2}");
        }
        else
        {
            return null; // Amount difference too large
        }

        // Date proximity (already filtered by grouping, so this is a bonus)
        var daysDifference = Math.Abs((debit.TransactionDate.Date - credit.TransactionDate.Date).TotalDays);
        if (daysDifference == 0)
        {
            confidence += 0.2m; // 20% for same day
            criteria.Add("Same day transaction");
        }
        else if (daysDifference <= 1)
        {
            confidence += 0.1m; // 10% for next day
            criteria.Add($"Within {daysDifference} day(s)");
        }

        // Description analysis
        var descriptionScore = AnalyzeDescriptions(debit.Description, credit.Description, debit.UserDescription, credit.UserDescription);
        confidence += descriptionScore.Score;
        criteria.AddRange(descriptionScore.Criteria);

        // Account name analysis (if accounts have meaningful names)
        var accountScore = AnalyzeAccountNames(debit.AccountName, credit.AccountName, debit.Description, credit.Description);
        confidence += accountScore.Score;
        criteria.AddRange(accountScore.Criteria);

        // Round amount bonus (transfers are often round numbers)
        if (IsRoundAmount(debitAmount))
        {
            confidence += 0.05m; // 5% bonus for round amounts
            criteria.Add("Round amount");
        }

        return new TransferCandidate
        {
            DebitTransaction = debit,
            CreditTransaction = credit,
            Amount = debitAmount,
            TransferDate = debit.TransactionDate.Date < credit.TransactionDate.Date ? debit.TransactionDate : credit.TransactionDate,
            ConfidenceScore = Math.Min(confidence, 1.0m), // Cap at 100%
            MatchingCriteria = criteria
        };
    }

    /// <summary>
    /// Analyzes transaction descriptions for transfer keywords
    /// </summary>
    private (decimal Score, List<string> Criteria) AnalyzeDescriptions(params string?[] descriptions)
    {
        var score = 0.0m;
        var criteria = new List<string>();
        var allText = string.Join(" ", descriptions.Where(d => !string.IsNullOrEmpty(d)));

        if (string.IsNullOrEmpty(allText))
            return (score, criteria);

        var comparison = _config.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        foreach (var keyword in _config.TransferKeywords)
        {
            if (allText.Contains(keyword, comparison))
            {
                score += 0.1m; // 10% per matching keyword (can stack)
                criteria.Add($"Contains keyword: {keyword}");
            }
        }

        // Special patterns
        if (allText.Contains("FROM", comparison) && allText.Contains("TO", comparison))
        {
            score += 0.1m;
            criteria.Add("Contains FROM/TO pattern");
        }

        return (Math.Min(score, 0.3m), criteria); // Cap description score at 30%
    }

    /// <summary>
    /// Analyzes account names for cross-references in descriptions
    /// </summary>
    private (decimal Score, List<string> Criteria) AnalyzeAccountNames(string debitAccount, string creditAccount, string debitDesc, string creditDesc)
    {
        var score = 0.0m;
        var criteria = new List<string>();

        if (string.IsNullOrEmpty(debitAccount) || string.IsNullOrEmpty(creditAccount))
            return (score, criteria);

        var comparison = _config.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        // Check if debit account is mentioned in credit description
        if (!string.IsNullOrEmpty(creditDesc) && creditDesc.Contains(debitAccount, comparison))
        {
            score += 0.1m;
            criteria.Add($"Credit description mentions source account: {debitAccount}");
        }

        // Check if credit account is mentioned in debit description
        if (!string.IsNullOrEmpty(debitDesc) && debitDesc.Contains(creditAccount, comparison))
        {
            score += 0.1m;
            criteria.Add($"Debit description mentions destination account: {creditAccount}");
        }

        return (score, criteria);
    }

    /// <summary>
    /// Checks if an amount is a "round" number (likely to be a manual transfer)
    /// </summary>
    private bool IsRoundAmount(decimal amount)
    {
        // Consider amounts ending in .00, .50, or multiples of 25/50/100 as "round"
        return amount % 1 == 0 || // Whole numbers
               amount % 0.50m == 0 || // Half dollars
               (amount >= 25 && amount % 25 == 0); // Multiples of 25
    }

    /// <summary>
    /// Removes duplicate candidates where the same transaction pair is matched
    /// </summary>
    private List<TransferCandidate> RemoveDuplicateCandidates(List<TransferCandidate> candidates)
    {
        return candidates
            .GroupBy(c => new { DebitId = c.DebitTransaction.Id, CreditId = c.CreditTransaction.Id })
            .Select(g => g.OrderByDescending(c => c.ConfidenceScore).First())
            .ToList();
    }
}