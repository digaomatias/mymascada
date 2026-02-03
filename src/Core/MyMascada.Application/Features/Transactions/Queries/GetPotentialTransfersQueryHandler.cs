using MediatR;
using AutoMapper;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Transactions.DTOs;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Transactions.Queries;

/// <summary>
/// Handler for finding potential transfer transactions
/// </summary>
public class GetPotentialTransfersQueryHandler : IRequestHandler<GetPotentialTransfersQuery, PotentialTransfersResponse>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IMapper _mapper;

    public GetPotentialTransfersQueryHandler(
        ITransactionRepository transactionRepository,
        IAccountRepository accountRepository,
        IMapper mapper)
    {
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
        _mapper = mapper;
    }

    public async Task<PotentialTransfersResponse> Handle(GetPotentialTransfersQuery request, CancellationToken cancellationToken)
    {
        // Get all user transactions that could be part of transfers
        var transactions = await _transactionRepository.GetUserTransactionsAsync(
            request.UserId, 
            includeDeleted: false,
            includeReviewed: request.IncludeReviewed,
            includeTransfers: request.IncludeExistingTransfers);

        var transactionDtos = transactions.Select(t => _mapper.Map<TransactionDto>(t)).ToList();
        var response = new PotentialTransfersResponse();

        // Find potential transfer pairs using simple binary matching
        var transferGroups = new List<TransferGroupDto>();
        var processedTransactions = new HashSet<int>();

        foreach (var sourceTransaction in transactionDtos)
        {
            if (processedTransactions.Contains(sourceTransaction.Id))
                continue;

            // Look for matching destination transactions with simple criteria
            var potentialMatches = FindSimpleMatches(sourceTransaction, transactionDtos);

            foreach (var match in potentialMatches)
            {
                if (processedTransactions.Contains(match.DestinationTransaction.Id))
                    continue;

                transferGroups.Add(match);
                processedTransactions.Add(sourceTransaction.Id);
                processedTransactions.Add(match.DestinationTransaction.Id);
                break; // Only take the first match for each source transaction
            }
        }

        // Always show unreviewed transactions for manual transfer marking
        var unmatchedTransfers = new List<UnmatchedTransferDto>();
        
        foreach (var transaction in transactionDtos)
        {
            if (processedTransactions.Contains(transaction.Id))
                continue;

            // Show all unreviewed transactions regardless of transfer likelihood
            if (!transaction.IsReviewed)
            {
                var unmatched = new UnmatchedTransferDto
                {
                    Transaction = transaction,
                    TransferConfidence = 1.0m, // Not used in UI anymore
                    TransferIndicators = GetSimpleTransferIndicators(transaction)
                };

                // Try to suggest destination account
                var suggestedAccount = SuggestDestinationAccount(transaction, transactionDtos);
                if (suggestedAccount != null)
                {
                    unmatched.SuggestedDestinationAccountId = suggestedAccount.Id;
                    unmatched.SuggestedDestinationAccountName = suggestedAccount.AccountName;
                }

                unmatchedTransfers.Add(unmatched);
            }
        }

        response.TransferGroups = transferGroups.ToList();
        response.UnmatchedTransfers = unmatchedTransfers.ToList();
        response.TotalGroups = transferGroups.Count;
        response.TotalUnmatched = unmatchedTransfers.Count;

        return response;
    }

    private List<TransferGroupDto> FindSimpleMatches(TransactionDto sourceTransaction, List<TransactionDto> allTransactions)
    {
        var matches = new List<TransferGroupDto>();
        var sourceAmount = Math.Abs(sourceTransaction.Amount);

        var candidateTransactions = allTransactions
            .Where(t => t.Id != sourceTransaction.Id)
            .Where(t => t.AccountId != sourceTransaction.AccountId) // Different accounts
            .Where(t => IsSimpleAmountMatch(sourceAmount, Math.Abs(t.Amount))) // Amount within $1
            .Where(t => IsSimpleDateMatch(sourceTransaction.TransactionDate, t.TransactionDate)); // Date within 3 days

        foreach (var candidateTransaction in candidateTransactions)
        {
            // Determine correct transfer direction based on transaction amounts
            // Source = outgoing (negative amount), Destination = incoming (positive amount)
            var sourceIsOutgoing = sourceTransaction.Amount < 0;
            var candidateIsOutgoing = candidateTransaction.Amount < 0;
            
            TransactionDto outgoingTransaction, incomingTransaction;
            
            if (sourceIsOutgoing && !candidateIsOutgoing)
            {
                // Source is outgoing, candidate is incoming - correct order
                outgoingTransaction = sourceTransaction;
                incomingTransaction = candidateTransaction;
            }
            else if (!sourceIsOutgoing && candidateIsOutgoing)
            {
                // Source is incoming, candidate is outgoing - swap order
                outgoingTransaction = candidateTransaction;
                incomingTransaction = sourceTransaction;
            }
            else
            {
                // Both same direction (both outgoing or both incoming) - skip this match as it's not a valid transfer
                continue;
            }
            
            var match = new TransferGroupDto
            {
                SourceTransaction = outgoingTransaction,      // Always the outgoing (negative) transaction
                DestinationTransaction = incomingTransaction, // Always the incoming (positive) transaction
                Confidence = 1.0m, // Not used in UI anymore
                Amount = sourceAmount,
                DateRange = FormatDateRange(sourceTransaction.TransactionDate, candidateTransaction.TransactionDate),
                MatchReasons = GetSimpleMatchReasons(sourceTransaction, candidateTransaction)
            };

            matches.Add(match);
        }

        return matches;
    }

    private decimal CalculateTransferConfidence(TransactionDto sourceTransaction, TransactionDto destinationTransaction)
    {
        decimal confidence = 0;

        // Amount similarity (40% weight) - Enhanced scoring
        var amountScore = CalculateAmountScore(Math.Abs(sourceTransaction.Amount), Math.Abs(destinationTransaction.Amount));
        confidence += amountScore * 0.4m;

        // Date proximity (30% weight) - Enhanced scoring with dynamic window
        var dateScore = CalculateDateScore(sourceTransaction.TransactionDate, destinationTransaction.TransactionDate, 
            Math.Abs(sourceTransaction.Amount), AreFromSameInstitution(sourceTransaction, destinationTransaction));
        confidence += dateScore * 0.3m;

        // Description similarity (20% weight) - Enhanced with transfer keywords
        var descriptionScore = CalculateEnhancedDescriptionScore(sourceTransaction.Description, destinationTransaction.Description);
        confidence += descriptionScore * 0.2m;

        // Account relationship (10% weight) - Same institution bonus
        var accountScore = AreFromSameInstitution(sourceTransaction, destinationTransaction) ? 1.0m : 0.5m;
        confidence += accountScore * 0.1m;

        return Math.Min(1.0m, confidence);
    }

    private decimal CalculateTransferLikelihood(TransactionDto transaction)
    {
        decimal likelihood = 0;

        var description = transaction.Description?.ToLowerInvariant() ?? "";
        var userDescription = transaction.UserDescription?.ToLowerInvariant() ?? "";
        var combinedDescription = $"{description} {userDescription}";

        // Transfer keywords (50% weight)
        var transferKeywords = new[] { "transfer", "moved", "internal", "between", "from", "to", "deposit", "withdrawal" };
        var keywordMatches = transferKeywords.Count(keyword => combinedDescription.Contains(keyword));
        likelihood += Math.Min(0.5m, keywordMatches * 0.15m);

        // Round amounts often indicate manual transfers (20% weight)
        var amount = Math.Abs(transaction.Amount);
        if (amount == Math.Round(amount) && amount >= 10) // Round number >= $10
        {
            likelihood += 0.2m;
        }

        // No category assigned (15% weight) - transfers often lack categories
        if (!transaction.CategoryId.HasValue)
        {
            likelihood += 0.15m;
        }

        // Large amounts (15% weight) - transfers often involve significant sums
        if (amount >= 100)
        {
            likelihood += 0.15m;
        }

        return Math.Min(1.0m, likelihood);
    }

    private decimal CalculateDescriptionSimilarity(string? desc1, string? desc2)
    {
        if (string.IsNullOrWhiteSpace(desc1) || string.IsNullOrWhiteSpace(desc2))
            return 0;

        desc1 = desc1.ToLowerInvariant();
        desc2 = desc2.ToLowerInvariant();

        // Simple word overlap similarity
        var words1 = desc1.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var words2 = desc2.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        if (words1.Count == 0 || words2.Count == 0)
            return 0;

        var intersection = words1.Intersect(words2).Count();
        var union = words1.Union(words2).Count();

        return (decimal)intersection / union;
    }

    private List<string> GetSimpleMatchReasons(TransactionDto source, TransactionDto destination)
    {
        var reasons = new List<string>();

        var sourceAmount = Math.Abs(source.Amount);
        var destAmount = Math.Abs(destination.Amount);

        if (sourceAmount == destAmount)
        {
            reasons.Add("Exact amount match");
        }

        var daysDiff = Math.Abs((destination.TransactionDate - source.TransactionDate).TotalDays);
        if (daysDiff == 0)
        {
            reasons.Add("Same day transactions");
        }
        else if (daysDiff <= 1)
        {
            reasons.Add("Consecutive day transactions");
        }
        else if (daysDiff <= 3)
        {
            reasons.Add("Within 3 days");
        }

        // Check for transfer keywords in descriptions
        var sourceDesc = source.Description?.ToLowerInvariant() ?? "";
        var destDesc = destination.Description?.ToLowerInvariant() ?? "";
        var transferKeywords = new[] { "transfer", "internal", "between", "moved" };
        
        if (transferKeywords.Any(keyword => sourceDesc.Contains(keyword) || destDesc.Contains(keyword)))
        {
            reasons.Add("Contains transfer keywords");
        }

        return reasons;
    }

    private List<string> GetSimpleTransferIndicators(TransactionDto transaction)
    {
        var indicators = new List<string>();

        var description = transaction.Description?.ToLowerInvariant() ?? "";
        var userDescription = transaction.UserDescription?.ToLowerInvariant() ?? "";
        var combinedDescription = $"{description} {userDescription}";

        var transferKeywords = new[] { "transfer", "internal", "between", "moved", "deposit", "withdrawal" };
        foreach (var keyword in transferKeywords)
        {
            if (combinedDescription.Contains(keyword))
            {
                indicators.Add($"Contains '{keyword}' keyword");
                break; // Only add one keyword indicator
            }
        }

        if (!transaction.CategoryId.HasValue)
        {
            indicators.Add("No category assigned");
        }

        if (Math.Abs(transaction.Amount) == Math.Round(Math.Abs(transaction.Amount)) && Math.Abs(transaction.Amount) >= 10)
        {
            indicators.Add("Round dollar amount");
        }

        return indicators;
    }

    private string FormatDateRange(DateTime date1, DateTime date2)
    {
        if (date1.Date == date2.Date)
        {
            return date1.ToString("MMM dd, yyyy");
        }

        var startDate = date1 < date2 ? date1 : date2;
        var endDate = date1 < date2 ? date2 : date1;

        return $"{startDate:MMM dd} - {endDate:MMM dd, yyyy}";
    }

    private TransactionDto? SuggestDestinationAccount(TransactionDto transaction, List<TransactionDto> allTransactions)
    {
        // Look for patterns in transaction descriptions to suggest likely destination accounts
        var description = transaction.Description?.ToLowerInvariant() ?? "";
        
        // Find accounts mentioned in similar transactions
        var accountNames = allTransactions
            .Where(t => t.AccountId != transaction.AccountId)
            .GroupBy(t => t.AccountId)
            .Select(g => new { AccountId = g.Key, AccountName = g.First().AccountName })
            .ToList();

        foreach (var account in accountNames)
        {
            if (description.Contains(account.AccountName?.ToLowerInvariant() ?? ""))
            {
                var dto = new TransactionDto();
                dto.Id = account.AccountId;
                dto.AccountName = account.AccountName ?? "";
                return dto;
            }
        }

        return null;
    }

    // Smart Transfer Detection Methods Based on AI Recommendations

    /// <summary>
    /// Enhanced amount matching with hybrid tolerance (percentage + fixed amount)
    /// </summary>
    private bool IsAmountMatch(decimal amount1, decimal amount2, decimal baseTolerance)
    {
        var percentageTolerance = Math.Max(amount1, amount2) * 0.005m; // 0.5%
        var fixedTolerance = Math.Max(1.0m, baseTolerance); // Minimum $1.00
        var tolerance = Math.Max(percentageTolerance, fixedTolerance);
        
        return Math.Abs(amount1 - amount2) <= tolerance;
    }

    /// <summary>
    /// Dynamic date window based on amount and institution relationship
    /// </summary>
    private bool IsWithinDateWindow(DateTime date1, DateTime date2, decimal amount, bool sameInstitution)
    {
        var maxDays = GetMaxDateDifference(amount, sameInstitution);
        var daysDiff = Math.Abs((date2 - date1).TotalDays);
        return daysDiff <= maxDays;
    }

    /// <summary>
    /// Calculate dynamic date window based on AI recommendations
    /// </summary>
    private int GetMaxDateDifference(decimal amount, bool sameInstitution)
    {
        var baseDays = sameInstitution ? 3 : 5;
        var amountFactor = Math.Min(Math.Abs(amount) / 1000m, 9); // Cap at +9 days
        var maxDays = (int)(baseDays + amountFactor);
        return Math.Min(maxDays, 14); // Absolute maximum 14 days
    }

    /// <summary>
    /// Check if transactions are from the same financial institution
    /// </summary>
    private bool AreFromSameInstitution(TransactionDto txn1, TransactionDto txn2)
    {
        // Simple heuristic: check if account names contain similar institution identifiers
        var name1 = txn1.AccountName?.ToUpperInvariant() ?? "";
        var name2 = txn2.AccountName?.ToUpperInvariant() ?? "";
        
        // Common institution patterns
        var institutions = new[] { "ANZ", "WESTPAC", "CBA", "NAB", "BANK", "CREDIT UNION" };
        
        foreach (var institution in institutions)
        {
            if (name1.Contains(institution) && name2.Contains(institution))
                return true;
        }
        
        return false;
    }

    /// <summary>
    /// Enhanced amount scoring with exponential decay
    /// </summary>
    private decimal CalculateAmountScore(decimal amount1, decimal amount2)
    {
        var difference = Math.Abs(amount1 - amount2);
        
        if (difference == 0)
            return 1.0m; // Perfect match
            
        var percentageTolerance = Math.Max(amount1, amount2) * 0.005m; // 0.5%
        var fixedTolerance = 1.0m;
        var maxTolerance = Math.Max(percentageTolerance, fixedTolerance);
        
        if (difference <= maxTolerance)
        {
            // Exponential decay for close matches
            var score = (decimal)Math.Exp(-(double)(difference / (maxTolerance / 2)));
            return Math.Max(0.8m, score); // Minimum 80% for matches within tolerance
        }
        
        return 0m; // No match
    }

    /// <summary>
    /// Enhanced date scoring with exponential decay
    /// </summary>
    private decimal CalculateDateScore(DateTime date1, DateTime date2, decimal amount, bool sameInstitution)
    {
        var maxWindow = GetMaxDateDifference(amount, sameInstitution);
        var daysDiff = Math.Abs((date2 - date1).TotalDays);
        
        if (daysDiff == 0)
            return 1.0m; // Same day = perfect score
            
        if (daysDiff <= maxWindow)
        {
            // Exponential decay: each day reduces score
            var score = (decimal)Math.Exp(-(double)(daysDiff / (maxWindow / 2.0)));
            return Math.Max(0.5m, score); // Minimum 50% for matches within window
        }
        
        return 0m; // Outside window
    }

    /// <summary>
    /// Enhanced description analysis with transfer keyword detection
    /// </summary>
    private decimal CalculateEnhancedDescriptionScore(string? desc1, string? desc2)
    {
        if (string.IsNullOrWhiteSpace(desc1) || string.IsNullOrWhiteSpace(desc2))
            return 0m;

        desc1 = desc1.ToLowerInvariant();
        desc2 = desc2.ToLowerInvariant();

        decimal score = 0;

        // Transfer keyword bonus
        var transferKeywords = new[] { "transfer", "xfer", "trsf", "internal", "between", "payment" };
        
        foreach (var keyword in transferKeywords)
        {
            if (desc1.Contains(keyword) || desc2.Contains(keyword))
            {
                score += 0.3m; // Significant bonus for transfer keywords
                break; // Only count once
            }
        }

        // String similarity (Jaccard similarity for word overlap)
        var words1 = desc1.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var words2 = desc2.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        if (words1.Count > 0 && words2.Count > 0)
        {
            var intersection = words1.Intersect(words2).Count();
            var union = words1.Union(words2).Count();
            var similarity = (decimal)intersection / union;
            score += similarity * 0.5m; // Word overlap contributes up to 50%
        }

        // Account name cross-reference bonus
        if (ContainsAccountReference(desc1, desc2))
        {
            score += 0.2m;
        }

        return Math.Min(1.0m, score);
    }

    /// <summary>
    /// Check if descriptions contain cross-references to account names
    /// </summary>
    private bool ContainsAccountReference(string desc1, string desc2)
    {
        // Look for common account reference patterns
        var accountPatterns = new[] { "visa", "go", "savings", "checking", "credit", "debit" };
        
        foreach (var pattern in accountPatterns)
        {
            if (desc1.Contains(pattern) && desc2.Contains(pattern))
                return true;
        }
        
        return false;
    }

    /// <summary>
    /// Simple amount matching - exact match only (no tolerance)
    /// Transfers must have matching amounts - money out equals money in
    /// </summary>
    private bool IsSimpleAmountMatch(decimal amount1, decimal amount2)
    {
        return amount1 == amount2;
    }

    /// <summary>
    /// Simple date matching - within 3 days
    /// </summary>
    private bool IsSimpleDateMatch(DateTime date1, DateTime date2)
    {
        var daysDiff = Math.Abs((date2 - date1).TotalDays);
        return daysDiff <= 3;
    }
}