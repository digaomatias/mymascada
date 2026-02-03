using AutoMapper;
using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Transactions.DTOs;

namespace MyMascada.Application.Features.Transactions.Queries;

public class GetDuplicateTransactionsQuery : IRequest<DuplicateTransactionsResponse>
{
    public Guid UserId { get; set; }
    public decimal AmountTolerance { get; set; } = 0.01m; // Allow small differences in amount
    public int DateToleranceDays { get; set; } = 1; // Allow 1 day difference
    public bool IncludeReviewed { get; set; } = false; // Include already reviewed transactions
    public bool SameAccountOnly { get; set; } = false; // Only check within same account
    public decimal MinConfidence { get; set; } = 0.5m; // Minimum confidence score
}

public class GetDuplicateTransactionsQueryHandler : IRequestHandler<GetDuplicateTransactionsQuery, DuplicateTransactionsResponse>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IDuplicateExclusionRepository _duplicateExclusionRepository;
    private readonly IMapper _mapper;

    public GetDuplicateTransactionsQueryHandler(
        ITransactionRepository transactionRepository,
        IDuplicateExclusionRepository duplicateExclusionRepository,
        IMapper mapper)
    {
        _transactionRepository = transactionRepository;
        _duplicateExclusionRepository = duplicateExclusionRepository;
        _mapper = mapper;
    }

    public async Task<DuplicateTransactionsResponse> Handle(GetDuplicateTransactionsQuery request, CancellationToken cancellationToken)
    {
        // Get all transactions for the user (excluding transfers for now)
        var allTransactions = await _transactionRepository.GetAllForDuplicateDetectionAsync(request.UserId, request.IncludeReviewed);
        
        // Get all exclusions for this user to filter out previously dismissed duplicates
        var exclusions = await _duplicateExclusionRepository.GetByUserIdAsync(request.UserId);
        
        var duplicateGroups = new List<DuplicateGroupDto>();
        var processedTransactionIds = new HashSet<int>();
        
        // Group transactions by potential duplicates
        foreach (var transaction in allTransactions)
        {
            if (processedTransactionIds.Contains(transaction.Id))
                continue;
                
            var potentialDuplicates = FindPotentialDuplicates(transaction, allTransactions, request);
            
            if (potentialDuplicates.Any())
            {
                // Check if this group has been previously excluded by the user
                var groupTransactionIds = new List<int> { transaction.Id }
                    .Concat(potentialDuplicates.Select(d => d.Transaction.Id))
                    .ToList();
                
                // Skip this group if it matches any exclusion
                var isExcluded = exclusions.Any(exclusion => exclusion.AppliesToTransactions(groupTransactionIds));
                
                if (!isExcluded)
                {
                    var group = new DuplicateGroupDto
                    {
                        Id = Guid.NewGuid(),
                        Transactions = new List<TransactionDto> { _mapper.Map<TransactionDto>(transaction) }
                            .Concat(potentialDuplicates.Select(d => _mapper.Map<TransactionDto>(d.Transaction)))
                            .ToList(),
                        HighestConfidence = potentialDuplicates.Max(d => d.Confidence),
                        TotalAmount = Math.Abs(transaction.Amount),
                        DateRange = $"{transaction.TransactionDate:MMM dd} - {potentialDuplicates.Max(d => d.Transaction.TransactionDate):MMM dd}",
                        Description = transaction.Description
                    };
                    
                    duplicateGroups.Add(group);
                }
                
                // Mark all transactions in this group as processed (whether excluded or not)
                processedTransactionIds.Add(transaction.Id);
                foreach (var duplicate in potentialDuplicates)
                {
                    processedTransactionIds.Add(duplicate.Transaction.Id);
                }
            }
        }
        
        // Sort by highest confidence first
        duplicateGroups = duplicateGroups
            .OrderByDescending(g => g.HighestConfidence)
            .ThenByDescending(g => g.TotalAmount)
            .ToList();
        
        return new DuplicateTransactionsResponse
        {
            DuplicateGroups = duplicateGroups,
            TotalGroups = duplicateGroups.Count,
            TotalTransactions = duplicateGroups.Sum(g => g.Transactions.Count),
            ProcessedAt = DateTime.UtcNow
        };
    }
    
    private List<DuplicateMatch> FindPotentialDuplicates(
        Domain.Entities.Transaction sourceTransaction, 
        List<Domain.Entities.Transaction> allTransactions,
        GetDuplicateTransactionsQuery request)
    {
        var duplicates = new List<DuplicateMatch>();
        
        foreach (var candidate in allTransactions)
        {
            // Skip self
            if (candidate.Id == sourceTransaction.Id)
                continue;
                
            // Skip if different accounts when same account filter is enabled
            if (request.SameAccountOnly && candidate.AccountId != sourceTransaction.AccountId)
                continue;
                
            // Apply hard filters before calculating confidence
            
            // Hard filter: Date tolerance
            var daysDifference = Math.Abs((sourceTransaction.TransactionDate.Date - candidate.TransactionDate.Date).Days);
            if (daysDifference > request.DateToleranceDays)
                continue;
                
            // Hard filter: Amount tolerance (if exact amount tolerance is set)
            var amountDifference = Math.Abs(sourceTransaction.Amount - candidate.Amount);
            if (amountDifference > request.AmountTolerance && amountDifference > Math.Abs(sourceTransaction.Amount) * 0.05m)
                continue; // Skip if amount difference exceeds both absolute tolerance and 5% relative tolerance
                
            var confidence = CalculateConfidenceScore(sourceTransaction, candidate, request);
            
            if (confidence >= request.MinConfidence)
            {
                duplicates.Add(new DuplicateMatch
                {
                    Transaction = candidate,
                    Confidence = confidence
                });
            }
        }
        
        return duplicates.OrderByDescending(d => d.Confidence).ToList();
    }
    
    private decimal CalculateConfidenceScore(
        Domain.Entities.Transaction source, 
        Domain.Entities.Transaction candidate,
        GetDuplicateTransactionsQuery request)
    {
        decimal score = 0m;
        
        // Amount similarity (40% weight) - candidates already pass hard filter
        var amountDifference = Math.Abs(source.Amount - candidate.Amount);
        if (amountDifference <= request.AmountTolerance)
        {
            score += 0.4m;
        }
        else if (amountDifference <= Math.Abs(source.Amount) * 0.05m) // 5% tolerance
        {
            score += 0.2m;
        }
        
        // Date proximity (30% weight) - candidates already pass hard filter
        var daysDifference = Math.Abs((source.TransactionDate.Date - candidate.TransactionDate.Date).Days);
        if (daysDifference == 0)
        {
            score += 0.3m;
        }
        else if (daysDifference <= request.DateToleranceDays)
        {
            score += 0.2m;
        }
        // Note: daysDifference > request.DateToleranceDays is already filtered out by hard filter
        
        // Description similarity (20% weight)
        var descriptionSimilarity = CalculateStringSimilarity(
            source.Description?.Trim() ?? "", 
            candidate.Description?.Trim() ?? "");
        if (descriptionSimilarity > 0.9m)
        {
            score += 0.2m;
        }
        else if (descriptionSimilarity > 0.7m)
        {
            score += 0.15m;
        }
        else if (descriptionSimilarity > 0.5m)
        {
            score += 0.1m;
        }
        
        // Same account bonus (10% weight)
        if (source.AccountId == candidate.AccountId)
        {
            score += 0.1m;
        }
        
        // Penalize if different external IDs (reduce confidence)
        if (!string.IsNullOrEmpty(source.ExternalId) && 
            !string.IsNullOrEmpty(candidate.ExternalId) && 
            source.ExternalId != candidate.ExternalId)
        {
            score -= 0.3m;
        }
        
        return Math.Max(0m, Math.Min(1m, score));
    }
    
    private decimal CalculateStringSimilarity(string str1, string str2)
    {
        if (string.IsNullOrEmpty(str1) && string.IsNullOrEmpty(str2))
            return 1m;
            
        if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
            return 0m;
            
        // Normalize strings: lowercase + normalize whitespace (remove extra spaces)
        var normalized1 = NormalizeString(str1);
        var normalized2 = NormalizeString(str2);
        
        // Simple Levenshtein distance-based similarity
        var distance = LevenshteinDistance(normalized1, normalized2);
        var maxLength = Math.Max(normalized1.Length, normalized2.Length);
        
        return 1m - (decimal)distance / maxLength;
    }
    
    private string NormalizeString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;
            
        // Convert to lowercase and normalize whitespace (replace multiple spaces with single space)
        return System.Text.RegularExpressions.Regex.Replace(input.ToLowerInvariant().Trim(), @"\s+", " ");
    }
    
    private int LevenshteinDistance(string s1, string s2)
    {
        if (s1.Length == 0) return s2.Length;
        if (s2.Length == 0) return s1.Length;
        
        var matrix = new int[s1.Length + 1, s2.Length + 1];
        
        for (int i = 0; i <= s1.Length; i++)
            matrix[i, 0] = i;
            
        for (int j = 0; j <= s2.Length; j++)
            matrix[0, j] = j;
            
        for (int i = 1; i <= s1.Length; i++)
        {
            for (int j = 1; j <= s2.Length; j++)
            {
                var cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }
        
        return matrix[s1.Length, s2.Length];
    }
}

internal class DuplicateMatch
{
    public Domain.Entities.Transaction Transaction { get; set; } = null!;
    public decimal Confidence { get; set; }
}