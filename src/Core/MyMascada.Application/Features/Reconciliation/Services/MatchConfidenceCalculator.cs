using MyMascada.Application.Features.Reconciliation.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Reconciliation.Services;

public interface IMatchConfidenceCalculator
{
    decimal CalculateMatchConfidence(Transaction systemTransaction, BankTransactionDto bankTransaction);
    MatchAnalysisDto AnalyzeMatch(Transaction systemTransaction, BankTransactionDto bankTransaction);
}

public class MatchConfidenceCalculator : IMatchConfidenceCalculator
{
    public decimal CalculateMatchConfidence(Transaction systemTransaction, BankTransactionDto bankTransaction)
    {
        var analysis = AnalyzeMatch(systemTransaction, bankTransaction);
        
        // Weighted scoring algorithm
        var amountScore = analysis.AmountMatch ? 1.0m : 
            (analysis.AmountDifference <= 0.01m ? 0.95m : 
             analysis.AmountDifference <= 1.0m ? 0.8m : 
             analysis.AmountDifference <= 5.0m ? 0.6m : 0.3m);
        
        var dateScore = analysis.DateDifferenceInDays switch
        {
            0 => 1.0m,
            1 => 0.9m,
            2 => 0.8m,
            <= 5 => 0.6m,
            <= 10 => 0.4m,
            _ => 0.1m
        };
        
        var descriptionScore = analysis.DescriptionSimilarityScore;
        
        // Weighted average: Amount (40%), Date (30%), Description (30%)
        var confidence = (amountScore * 0.4m) + (dateScore * 0.3m) + (descriptionScore * 0.3m);
        
        // Apply penalties for significant differences
        if (analysis.AmountDifference > 10.0m) confidence *= 0.7m;
        if (analysis.DateDifferenceInDays > 7) confidence *= 0.8m;
        if (analysis.DescriptionSimilarityScore < 0.3m) confidence *= 0.9m;
        
        return Math.Max(0, Math.Min(1, confidence));
    }
    
    public MatchAnalysisDto AnalyzeMatch(Transaction systemTransaction, BankTransactionDto bankTransaction)
    {
        var amountDiff = Math.Abs(systemTransaction.Amount - bankTransaction.Amount);
        var systemDate = systemTransaction.TransactionDate.Date;
        var bankDate = bankTransaction.TransactionDate.Date;
        var dateDiff = Math.Abs((systemDate - bankDate).Days);
        
        var descriptionSimilarity = CalculateDescriptionSimilarity(
            systemTransaction.Description, 
            bankTransaction.Description
        );
        
        return new MatchAnalysisDto
        {
            AmountMatch = amountDiff < 0.01m,
            AmountDifference = amountDiff,
            DateMatch = dateDiff == 0,
            DateDifferenceInDays = dateDiff,
            DescriptionSimilar = descriptionSimilarity > 0.5m,
            DescriptionSimilarityScore = descriptionSimilarity,
            SystemAmount = systemTransaction.Amount,
            BankAmount = bankTransaction.Amount,
            SystemDate = systemDate,
            BankDate = bankDate,
            SystemDescription = systemTransaction.Description,
            BankDescription = bankTransaction.Description
        };
    }
    
    private decimal CalculateDescriptionSimilarity(string desc1, string desc2)
    {
        if (string.IsNullOrWhiteSpace(desc1) || string.IsNullOrWhiteSpace(desc2))
            return 0;
        
        // Normalize descriptions
        var normalized1 = NormalizeDescription(desc1);
        var normalized2 = NormalizeDescription(desc2);
        
        // Exact match
        if (normalized1 == normalized2) return 1.0m;
        
        // Contains check
        if (normalized1.Contains(normalized2) || normalized2.Contains(normalized1))
            return 0.9m;
        
        // Word overlap scoring
        var words1 = normalized1.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var words2 = normalized2.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        var commonWords = words1.Intersect(words2).Count();
        var totalWords = Math.Max(words1.Length, words2.Length);
        
        if (totalWords == 0) return 0;
        
        var wordOverlapScore = (decimal)commonWords / totalWords;
        
        // Levenshtein distance for character-level similarity
        var levenshteinDistance = CalculateLevenshteinDistance(normalized1, normalized2);
        var maxLength = Math.Max(normalized1.Length, normalized2.Length);
        var characterSimilarity = maxLength > 0 ? 1.0m - ((decimal)levenshteinDistance / maxLength) : 0;
        
        // Combined score: word overlap (70%) + character similarity (30%)
        return (wordOverlapScore * 0.7m) + (characterSimilarity * 0.3m);
    }
    
    private string NormalizeDescription(string description)
    {
        return description.ToLowerInvariant()
            .Replace("&", "and")
            .Replace("-", " ")
            .Replace("_", " ")
            .Replace(".", " ")
            .Replace(",", " ")
            .Replace("  ", " ")
            .Trim();
    }
    
    private int CalculateLevenshteinDistance(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1)) return s2?.Length ?? 0;
        if (string.IsNullOrEmpty(s2)) return s1.Length;
        
        var matrix = new int[s1.Length + 1, s2.Length + 1];
        
        for (int i = 0; i <= s1.Length; i++)
            matrix[i, 0] = i;
        
        for (int j = 0; j <= s2.Length; j++)
            matrix[0, j] = j;
        
        for (int i = 1; i <= s1.Length; i++)
        {
            for (int j = 1; j <= s2.Length; j++)
            {
                int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost
                );
            }
        }
        
        return matrix[s1.Length, s2.Length];
    }
}

public record MatchAnalysisDto
{
    public bool AmountMatch { get; init; }
    public decimal AmountDifference { get; init; }
    public bool DateMatch { get; init; }
    public int DateDifferenceInDays { get; init; }
    public bool DescriptionSimilar { get; init; }
    public decimal DescriptionSimilarityScore { get; init; }
    public decimal SystemAmount { get; init; }
    public decimal BankAmount { get; init; }
    public DateTime SystemDate { get; init; }
    public DateTime BankDate { get; init; }
    public string SystemDescription { get; init; } = string.Empty;
    public string BankDescription { get; init; } = string.Empty;
}