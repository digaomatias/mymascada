using MyMascada.Application.Features.Reconciliation.DTOs;
using MyMascada.Application.Features.Reconciliation.Services;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace MyMascada.Infrastructure.Services.Reconciliation;

public class TransactionMatchingService : ITransactionMatchingService
{
    private readonly ILogger<TransactionMatchingService> _logger;

    public TransactionMatchingService(ILogger<TransactionMatchingService> logger)
    {
        _logger = logger;
    }
    public async Task<MatchingResultDto> MatchTransactionsAsync(
        TransactionMatchRequest request, 
        IEnumerable<Transaction> appTransactions)
    {
        var appTransactionsList = appTransactions.ToList();
        var bankTransactionsList = request.BankTransactions.ToList();
        
        _logger.LogDebug("Starting reconciliation matching with {BankCount} bank transactions and {AppCount} app transactions", 
            bankTransactionsList.Count, appTransactionsList.Count);
        
        _logger.LogDebug("Tolerance settings - Amount: {Amount}, Description: {Desc}, DateRange: {DateRange} days", 
            request.ToleranceAmount ?? 0.01m, request.UseDescriptionMatching, request.DateRangeToleranceDays);
        
        // Log sample transactions for debugging
        if (bankTransactionsList.Any())
        {
            var firstBank = bankTransactionsList.First();
            _logger.LogDebug("Sample bank transaction: ID={Id}, Amount={Amount}, Date={Date}, Desc='{Desc}'", 
                firstBank.BankTransactionId, firstBank.Amount, firstBank.TransactionDate, firstBank.Description);
        }
        
        if (appTransactionsList.Any())
        {
            var firstApp = appTransactionsList.First();
            _logger.LogDebug("Sample app transaction: ID={Id}, Amount={Amount}, Date={Date}, Desc='{Desc}'", 
                firstApp.Id, firstApp.Amount, firstApp.TransactionDate, firstApp.Description);
        }
        
        var matchedPairs = new List<MatchedPairDto>();
        var unmatchedBankTransactions = new List<BankTransactionDto>(bankTransactionsList);
        var unmatchedAppTransactions = new List<Transaction>(appTransactionsList);
        
        // Track already matched transactions to prevent double-matching
        var matchedAppTransactionIds = new HashSet<int>();
        var matchedBankTransactionIds = new HashSet<string>();

        // Improved algorithm: Find globally optimal matches
        await PerformOptimalMatching(
            unmatchedBankTransactions, 
            unmatchedAppTransactions, 
            matchedPairs,
            matchedAppTransactionIds,
            matchedBankTransactionIds,
            request.ToleranceAmount ?? 0.01m,
            request.UseDescriptionMatching,
            request.UseDateRangeMatching,
            request.DateRangeToleranceDays);

        // Validate that no transaction is matched twice
        ValidateNoDuplicateMatches(matchedPairs);

        var exactMatches = matchedPairs.Count(p => p.MatchMethod == MatchMethod.Exact);
        var fuzzyMatches = matchedPairs.Count(p => p.MatchMethod == MatchMethod.Fuzzy);
        var totalTransactions = bankTransactionsList.Count + appTransactionsList.Count;
        var overallMatchPercentage = totalTransactions > 0 
            ? (decimal)(matchedPairs.Count * 2) / totalTransactions * 100 
            : 0;

        _logger.LogInformation("Reconciliation matching completed for {ReconciliationId}: " +
            "{ExactMatches} exact matches, {FuzzyMatches} fuzzy matches, " +
            "{UnmatchedBank} unmatched bank transactions, {UnmatchedApp} unmatched app transactions. " +
            "Overall match rate: {MatchPercentage:F1}%",
            request.ReconciliationId, exactMatches, fuzzyMatches, 
            unmatchedBankTransactions.Count, unmatchedAppTransactions.Count, overallMatchPercentage);

        if (unmatchedBankTransactions.Any() || unmatchedAppTransactions.Any())
        {
            _logger.LogWarning("Reconciliation has unmatched transactions that may need manual review");
            
            if (unmatchedBankTransactions.Any())
            {
                _logger.LogInformation("Unmatched bank transactions ({Count}):", unmatchedBankTransactions.Count);
                foreach (var unmatched in unmatchedBankTransactions.Take(3))
                {
                    _logger.LogInformation("  - {Id}: {Amount:C} on {Date:yyyy-MM-dd} | '{Description}'",
                        unmatched.BankTransactionId, unmatched.Amount, unmatched.TransactionDate, unmatched.Description);
                }
                if (unmatchedBankTransactions.Count > 3)
                    _logger.LogInformation("  ... and {More} more", unmatchedBankTransactions.Count - 3);
            }
            
            if (unmatchedAppTransactions.Any())
            {
                _logger.LogInformation("Unmatched app transactions ({Count}):", unmatchedAppTransactions.Count);
                foreach (var unmatched in unmatchedAppTransactions.Take(3))
                {
                    _logger.LogInformation("  - {Id}: {Amount:C} on {Date:yyyy-MM-dd} | '{Description}'",
                        unmatched.Id, unmatched.Amount, unmatched.TransactionDate, unmatched.Description);
                }
                if (unmatchedAppTransactions.Count > 3)
                    _logger.LogInformation("  ... and {More} more", unmatchedAppTransactions.Count - 3);
            }
        }

        return new MatchingResultDto
        {
            ReconciliationId = request.ReconciliationId,
            TotalBankTransactions = bankTransactionsList.Count,
            TotalAppTransactions = appTransactionsList.Count,
            ExactMatches = exactMatches,
            FuzzyMatches = fuzzyMatches,
            UnmatchedBank = unmatchedBankTransactions.Count,
            UnmatchedApp = unmatchedAppTransactions.Count,
            OverallMatchPercentage = overallMatchPercentage,
            MatchedPairs = matchedPairs,
            UnmatchedBankTransactions = unmatchedBankTransactions,
            UnmatchedAppTransactions = unmatchedAppTransactions.Select(t => new TransactionDetailsDto
            {
                Id = t.Id,
                Amount = t.Amount,
                Description = t.Description,
                TransactionDate = t.TransactionDate,
                CategoryName = t.Category?.Name,
                Status = t.Status
            })
        };
    }

    public async Task<MatchedPairDto?> FindBestMatchAsync(
        BankTransactionDto bankTransaction, 
        IEnumerable<Transaction> appTransactions,
        decimal toleranceAmount = 0.01m,
        bool useDescriptionMatching = true,
        bool useDateRangeMatching = true,
        int dateRangeToleranceDays = 2)
    {
        var candidates = appTransactions
            .Where(t => Math.Abs(t.Amount - bankTransaction.Amount) <= toleranceAmount)
            .ToList();

        if (!candidates.Any())
            return null;

        var bestMatch = candidates
            .Select(t => new
            {
                Transaction = t,
                Confidence = CalculateMatchConfidence(bankTransaction, t, useDescriptionMatching, useDateRangeMatching, dateRangeToleranceDays)
            })
            .OrderByDescending(x => x.Confidence)
            .First();

        if (bestMatch.Confidence < 0.5m) // Minimum confidence threshold
            return null;

        var matchMethod = bestMatch.Confidence >= 0.95m ? MatchMethod.Exact : MatchMethod.Fuzzy;
        var matchReason = GenerateMatchReason(bankTransaction, bestMatch.Transaction, bestMatch.Confidence);

        return new MatchedPairDto
        {
            BankTransaction = bankTransaction,
            AppTransaction = new TransactionDetailsDto
            {
                Id = bestMatch.Transaction.Id,
                Amount = bestMatch.Transaction.Amount,
                Description = bestMatch.Transaction.Description,
                TransactionDate = bestMatch.Transaction.TransactionDate,
                CategoryName = bestMatch.Transaction.Category?.Name,
                Status = bestMatch.Transaction.Status
            },
            MatchConfidence = bestMatch.Confidence,
            MatchMethod = matchMethod,
            MatchReason = matchReason
        };
    }

    public decimal CalculateMatchConfidence(
        BankTransactionDto bankTransaction, 
        Transaction appTransaction,
        bool useDescriptionMatching = true,
        bool useDateRangeMatching = true,
        int dateRangeToleranceDays = 2)
    {
        var confidence = 0.0m;
        var factors = 0;

        // Amount matching (most important factor)
        var amountDifference = Math.Abs(bankTransaction.Amount - appTransaction.Amount);
        var amountConfidence = amountDifference == 0 ? 1.0m : Math.Max(0, 1.0m - (amountDifference * 10)); // Penalty for amount differences
        confidence += amountConfidence * 0.4m; // 40% weight
        factors++;

        // Date matching
        if (useDateRangeMatching)
        {
            var dateDifference = Math.Abs((bankTransaction.TransactionDate.Date - appTransaction.TransactionDate.Date).Days);
            var dateConfidence = dateDifference <= dateRangeToleranceDays 
                ? Math.Max(0, 1.0m - (dateDifference * 0.1m))
                : 0;
            confidence += dateConfidence * 0.3m; // 30% weight
            factors++;
        }

        // Description matching
        if (useDescriptionMatching)
        {
            var descriptionConfidence = CalculateDescriptionSimilarity(bankTransaction.Description, appTransaction.Description);
            confidence += descriptionConfidence * 0.3m; // 30% weight
            factors++;
        }

        return factors > 0 ? confidence / factors * factors : 0; // Normalize but keep full weight
    }

    private async Task PerformOptimalMatching(
        List<BankTransactionDto> bankTransactions,
        List<Transaction> appTransactions,
        List<MatchedPairDto> matchedPairs,
        HashSet<int> matchedAppTransactionIds,
        HashSet<string> matchedBankTransactionIds,
        decimal toleranceAmount,
        bool useDescriptionMatching,
        bool useDateRangeMatching,
        int dateRangeToleranceDays)
    {
        _logger.LogDebug("Starting optimal matching with {BankCount} bank transactions and {AppCount} app transactions", 
            bankTransactions.Count, appTransactions.Count);

        // Step 1: Calculate all possible matches with confidence scores
        var allPossibleMatches = new List<PotentialMatch>();

        foreach (var bankTx in bankTransactions)
        {
            var candidatesForBank = 0;
            foreach (var appTx in appTransactions)
            {
                // Basic amount check first
                if (Math.Abs(appTx.Amount - bankTx.Amount) <= toleranceAmount)
                {
                    var confidence = CalculateMatchConfidence(bankTx, appTx, useDescriptionMatching, useDateRangeMatching, dateRangeToleranceDays);
                    
                    if (confidence >= 0.5m) // Minimum threshold
                    {
                        candidatesForBank++;
                        var matchMethod = DetermineMatchMethod(bankTx, appTx, confidence);
                        var isExact = IsExactMatch(bankTx, appTx, toleranceAmount);
                        
                        allPossibleMatches.Add(new PotentialMatch
                        {
                            BankTransaction = bankTx,
                            AppTransaction = appTx,
                            Confidence = confidence,
                            MatchMethod = matchMethod,
                            IsExactMatch = isExact
                        });
                        
                        _logger.LogTrace("Found potential match: Bank={BankId}({BankAmount},{BankDate},'{BankDesc}') -> App={AppId}({AppAmount},{AppDate},'{AppDesc}') | Confidence={Confidence:F2}, Method={Method}, Exact={IsExact}",
                            bankTx.BankTransactionId, bankTx.Amount, bankTx.TransactionDate.ToString("yyyy-MM-dd"), bankTx.Description?.Substring(0, Math.Min(20, bankTx.Description?.Length ?? 0)),
                            appTx.Id, appTx.Amount, appTx.TransactionDate.ToString("yyyy-MM-dd"), appTx.Description?.Substring(0, Math.Min(20, appTx.Description?.Length ?? 0)),
                            confidence, matchMethod, isExact);
                    }
                }
            }
            
            if (candidatesForBank == 0)
            {
                _logger.LogDebug("No candidates found for bank transaction: {BankId}({Amount},{Date},'{Desc}')",
                    bankTx.BankTransactionId, bankTx.Amount, bankTx.TransactionDate.ToString("yyyy-MM-dd"), 
                    bankTx.Description?.Substring(0, Math.Min(30, bankTx.Description?.Length ?? 0)));
            }
        }

        _logger.LogDebug("Found {TotalMatches} potential matches ({ExactCount} exact, {FuzzyCount} fuzzy)",
            allPossibleMatches.Count,
            allPossibleMatches.Count(m => m.IsExactMatch),
            allPossibleMatches.Count(m => !m.IsExactMatch));

        // Step 2: Sort by priority - exact matches first, then by confidence
        var sortedMatches = allPossibleMatches
            .OrderByDescending(m => m.IsExactMatch ? 1 : 0) // Exact matches first
            .ThenByDescending(m => m.Confidence) // Then by confidence
            .ToList();

        _logger.LogDebug("Top 5 potential matches after sorting:");
        foreach (var match in sortedMatches.Take(5))
        {
            _logger.LogDebug("  {Priority}: Bank={BankId} -> App={AppId} | Confidence={Confidence:F2}, Method={Method}, Exact={IsExact}",
                sortedMatches.IndexOf(match) + 1, match.BankTransaction.BankTransactionId, match.AppTransaction.Id,
                match.Confidence, match.MatchMethod, match.IsExactMatch);
        }

        // Step 3: Greedily assign best matches while avoiding conflicts
        var bankToRemove = new List<BankTransactionDto>();
        var appToRemove = new List<Transaction>();
        var matchesCreated = 0;
        var matchesSkipped = 0;

        foreach (var match in sortedMatches)
        {
            // Skip if either transaction is already matched
            if (matchedBankTransactionIds.Contains(match.BankTransaction.BankTransactionId) ||
                matchedAppTransactionIds.Contains(match.AppTransaction.Id))
            {
                matchesSkipped++;
                _logger.LogTrace("Skipping potential match (already matched): Bank={BankId} -> App={AppId}",
                    match.BankTransaction.BankTransactionId, match.AppTransaction.Id);
                continue;
            }

            // Create the match
            var matchedPair = new MatchedPairDto
            {
                BankTransaction = match.BankTransaction,
                AppTransaction = new TransactionDetailsDto
                {
                    Id = match.AppTransaction.Id,
                    Amount = match.AppTransaction.Amount,
                    Description = match.AppTransaction.Description,
                    TransactionDate = match.AppTransaction.TransactionDate,
                    CategoryName = match.AppTransaction.Category?.Name,
                    Status = match.AppTransaction.Status
                },
                MatchConfidence = match.Confidence,
                MatchMethod = match.MatchMethod,
                MatchReason = GenerateMatchReason(match.BankTransaction, match.AppTransaction, match.Confidence)
            };

            matchedPairs.Add(matchedPair);
            matchesCreated++;

            // Track matched transactions
            matchedAppTransactionIds.Add(match.AppTransaction.Id);
            matchedBankTransactionIds.Add(match.BankTransaction.BankTransactionId);
            
            bankToRemove.Add(match.BankTransaction);
            appToRemove.Add(match.AppTransaction);
            
            _logger.LogDebug("Created match #{MatchNum}: Bank={BankId}({BankAmount}) -> App={AppId}({AppAmount}) | {Method}, Confidence={Confidence:F2}",
                matchesCreated, match.BankTransaction.BankTransactionId, match.BankTransaction.Amount,
                match.AppTransaction.Id, match.AppTransaction.Amount, match.MatchMethod, match.Confidence);
        }

        _logger.LogDebug("Matching completed: {Created} matches created, {Skipped} potential matches skipped due to conflicts", 
            matchesCreated, matchesSkipped);

        // Remove matched transactions from the unmatched lists
        var bankRemoved = bankTransactions.RemoveAll(b => bankToRemove.Contains(b));
        var appRemoved = appTransactions.RemoveAll(a => appToRemove.Contains(a));
        
        _logger.LogDebug("Removed {BankRemoved} bank transactions and {AppRemoved} app transactions from unmatched lists",
            bankRemoved, appRemoved);
        
        _logger.LogDebug("Final unmatched counts: {UnmatchedBank} bank transactions, {UnmatchedApp} app transactions",
            bankTransactions.Count, appTransactions.Count);

        if (bankTransactions.Any())
        {
            _logger.LogDebug("Unmatched bank transactions:");
            foreach (var unmatchedBank in bankTransactions.Take(5))
            {
                _logger.LogDebug("  Bank {Id}: {Amount} on {Date} - '{Desc}'",
                    unmatchedBank.BankTransactionId, unmatchedBank.Amount, unmatchedBank.TransactionDate.ToString("yyyy-MM-dd"),
                    unmatchedBank.Description?.Substring(0, Math.Min(40, unmatchedBank.Description?.Length ?? 0)));
            }
            if (bankTransactions.Count > 5)
                _logger.LogDebug("  ... and {More} more unmatched bank transactions", bankTransactions.Count - 5);
        }

        if (appTransactions.Any())
        {
            _logger.LogDebug("Unmatched app transactions:");
            foreach (var unmatchedApp in appTransactions.Take(5))
            {
                _logger.LogDebug("  App {Id}: {Amount} on {Date} - '{Desc}'",
                    unmatchedApp.Id, unmatchedApp.Amount, unmatchedApp.TransactionDate.ToString("yyyy-MM-dd"),
                    unmatchedApp.Description?.Substring(0, Math.Min(40, unmatchedApp.Description?.Length ?? 0)));
            }
            if (appTransactions.Count > 5)
                _logger.LogDebug("  ... and {More} more unmatched app transactions", appTransactions.Count - 5);
        }

        await Task.CompletedTask;
    }

    private bool IsExactMatch(BankTransactionDto bankTx, Transaction appTx, decimal toleranceAmount)
    {
        return Math.Abs(bankTx.Amount - appTx.Amount) <= toleranceAmount &&
               bankTx.TransactionDate.Date == appTx.TransactionDate.Date &&
               CalculateDescriptionSimilarity(bankTx.Description, appTx.Description) >= 0.8m;
    }

    private MatchMethod DetermineMatchMethod(BankTransactionDto bankTx, Transaction appTx, decimal confidence)
    {
        // More precise criteria for exact vs fuzzy
        var amountMatch = Math.Abs(bankTx.Amount - appTx.Amount) < 0.01m;
        var dateMatch = bankTx.TransactionDate.Date == appTx.TransactionDate.Date;
        var descriptionSimilarity = CalculateDescriptionSimilarity(bankTx.Description, appTx.Description);
        
        // Exact match: perfect amount, same date, and high description similarity
        if (amountMatch && dateMatch && descriptionSimilarity >= 0.8m)
            return MatchMethod.Exact;
        
        // Also consider as exact if extremely high confidence (>= 0.95)
        if (confidence >= 0.95m)
            return MatchMethod.Exact;
            
        return MatchMethod.Fuzzy;
    }

    private class PotentialMatch
    {
        public BankTransactionDto BankTransaction { get; set; } = null!;
        public Transaction AppTransaction { get; set; } = null!;
        public decimal Confidence { get; set; }
        public MatchMethod MatchMethod { get; set; }
        public bool IsExactMatch { get; set; }
    }

    private async Task PerformExactMatching(
        List<BankTransactionDto> bankTransactions,
        List<Transaction> appTransactions,
        List<MatchedPairDto> matchedPairs,
        HashSet<int> matchedAppTransactionIds,
        HashSet<string> matchedBankTransactionIds,
        decimal toleranceAmount)
    {
        var bankToRemove = new List<BankTransactionDto>();
        var appToRemove = new List<Transaction>();

        foreach (var bankTx in bankTransactions.ToList())
        {
            // Skip if this bank transaction is already matched
            if (matchedBankTransactionIds.Contains(bankTx.BankTransactionId))
                continue;

            var exactMatch = appTransactions.FirstOrDefault(appTx =>
                !matchedAppTransactionIds.Contains(appTx.Id) && // Ensure not already matched
                Math.Abs(appTx.Amount - bankTx.Amount) <= toleranceAmount &&
                appTx.TransactionDate.Date == bankTx.TransactionDate.Date &&
                CalculateDescriptionSimilarity(bankTx.Description, appTx.Description) >= 0.8m);

            if (exactMatch != null)
            {
                matchedPairs.Add(new MatchedPairDto
                {
                    BankTransaction = bankTx,
                    AppTransaction = new TransactionDetailsDto
                    {
                        Id = exactMatch.Id,
                        Amount = exactMatch.Amount,
                        Description = exactMatch.Description,
                        TransactionDate = exactMatch.TransactionDate,
                        CategoryName = exactMatch.Category?.Name,
                        Status = exactMatch.Status
                    },
                    MatchConfidence = 1.0m,
                    MatchMethod = MatchMethod.Exact,
                    MatchReason = "Exact match: amount, date, and description"
                });

                // Track matched transactions to prevent double-matching
                matchedAppTransactionIds.Add(exactMatch.Id);
                matchedBankTransactionIds.Add(bankTx.BankTransactionId);
                
                bankToRemove.Add(bankTx);
                appToRemove.Add(exactMatch);
            }
        }

        // Remove matched transactions
        bankTransactions.RemoveAll(b => bankToRemove.Contains(b));
        appTransactions.RemoveAll(a => appToRemove.Contains(a));

        await Task.CompletedTask;
    }

    private async Task PerformFuzzyMatching(
        List<BankTransactionDto> bankTransactions,
        List<Transaction> appTransactions,
        List<MatchedPairDto> matchedPairs,
        HashSet<int> matchedAppTransactionIds,
        HashSet<string> matchedBankTransactionIds,
        decimal toleranceAmount,
        bool useDescriptionMatching,
        bool useDateRangeMatching,
        int dateRangeToleranceDays)
    {
        var bankToRemove = new List<BankTransactionDto>();
        var appToRemove = new List<Transaction>();

        foreach (var bankTx in bankTransactions.ToList())
        {
            // Skip if this bank transaction is already matched
            if (matchedBankTransactionIds.Contains(bankTx.BankTransactionId))
                continue;

            // Filter out already matched app transactions before finding best match
            var availableAppTransactions = appTransactions
                .Where(t => !matchedAppTransactionIds.Contains(t.Id))
                .ToList();

            var bestMatch = await FindBestMatchAsync(
                bankTx,
                availableAppTransactions,
                toleranceAmount,
                useDescriptionMatching,
                useDateRangeMatching,
                dateRangeToleranceDays);

            if (bestMatch != null)
            {
                // Double-check that this transaction hasn't been matched already
                if (matchedAppTransactionIds.Contains(bestMatch.AppTransaction.Id))
                    continue;

                matchedPairs.Add(bestMatch);
                
                // Track matched transactions to prevent double-matching
                matchedAppTransactionIds.Add(bestMatch.AppTransaction.Id);
                matchedBankTransactionIds.Add(bankTx.BankTransactionId);
                
                bankToRemove.Add(bankTx);

                var matchedAppTx = appTransactions.First(t => t.Id == bestMatch.AppTransaction.Id);
                appToRemove.Add(matchedAppTx);
            }
        }

        // Remove matched transactions
        bankTransactions.RemoveAll(b => bankToRemove.Contains(b));
        appTransactions.RemoveAll(a => appToRemove.Contains(a));
    }

    private decimal CalculateDescriptionSimilarity(string description1, string description2)
    {
        if (string.IsNullOrWhiteSpace(description1) || string.IsNullOrWhiteSpace(description2))
            return 0;

        // Normalize descriptions
        var norm1 = NormalizeDescription(description1);
        var norm2 = NormalizeDescription(description2);

        // Exact match
        if (norm1.Equals(norm2, StringComparison.OrdinalIgnoreCase))
            return 1.0m;

        // Contains match
        if (norm1.Contains(norm2, StringComparison.OrdinalIgnoreCase) || 
            norm2.Contains(norm1, StringComparison.OrdinalIgnoreCase))
            return 0.8m;

        // Word-based similarity
        var words1 = norm1.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var words2 = norm2.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var commonWords = words1.Intersect(words2, StringComparer.OrdinalIgnoreCase).Count();
        var totalWords = Math.Max(words1.Length, words2.Length);

        return totalWords > 0 ? (decimal)commonWords / totalWords * 0.6m : 0;
    }

    private string NormalizeDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return string.Empty;

        // Remove common banking codes and references
        var normalized = Regex.Replace(description, @"[^a-zA-Z0-9\s]", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ");
        normalized = normalized.Trim().ToUpperInvariant();

        // Remove common bank terms
        var commonTerms = new[] { "EFTPOS", "PAYPAL", "TRANSFER", "PAYMENT", "WITHDRAW", "DEPOSIT" };
        foreach (var term in commonTerms)
        {
            normalized = normalized.Replace(term, "");
        }

        return Regex.Replace(normalized, @"\s+", " ").Trim();
    }

    private string GenerateMatchReason(BankTransactionDto bankTx, Transaction appTx, decimal confidence)
    {
        var reasons = new List<string>();

        var amountMatch = Math.Abs(bankTx.Amount - appTx.Amount) < 0.01m;
        var dateMatch = Math.Abs((bankTx.TransactionDate.Date - appTx.TransactionDate.Date).Days) <= 1;
        var descMatch = CalculateDescriptionSimilarity(bankTx.Description, appTx.Description) >= 0.7m;

        if (amountMatch) reasons.Add("amount");
        if (dateMatch) reasons.Add("date");
        if (descMatch) reasons.Add("description");

        var reason = reasons.Any() 
            ? $"Matched on: {string.Join(", ", reasons)}"
            : "Partial match";

        return $"{reason} (confidence: {confidence:P0})";
    }

    private void ValidateNoDuplicateMatches(List<MatchedPairDto> matchedPairs)
    {
        // Check for duplicate app transactions
        var appTransactionIds = matchedPairs.Select(p => p.AppTransaction.Id).ToList();
        var duplicateAppIds = appTransactionIds.GroupBy(id => id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateAppIds.Any())
        {
            throw new InvalidOperationException($"Duplicate app transaction matches found for transaction IDs: {string.Join(", ", duplicateAppIds)}");
        }

        // Check for duplicate bank transactions
        var bankTransactionIds = matchedPairs.Select(p => p.BankTransaction.BankTransactionId).ToList();
        var duplicateBankIds = bankTransactionIds.GroupBy(id => id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateBankIds.Any())
        {
            throw new InvalidOperationException($"Duplicate bank transaction matches found for transaction IDs: {string.Join(", ", duplicateBankIds)}");
        }
    }
}
