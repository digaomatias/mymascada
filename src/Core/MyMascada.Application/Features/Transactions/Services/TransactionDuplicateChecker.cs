using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Transactions.Commands;
using MyMascada.Domain.Entities;
using System.Security.Cryptography;
using System.Text;

namespace MyMascada.Application.Features.Transactions.Services;

public class TransactionDuplicateChecker
{
    private readonly ITransactionRepository _transactionRepository;

    public TransactionDuplicateChecker(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    public async Task<Transaction?> CheckForDuplicatesAsync(CreateTransactionCommand request)
    {
        // For manual transactions, only prevent rapid double-clicks (2 seconds)
        // Users should be able to create multiple transactions with same amount/description
        // as they might represent legitimate separate transactions (e.g., buying coffee twice)
        
        // Only check for very recent duplicates to prevent accidental double-clicks
        var recentDuplicate = await _transactionRepository.GetRecentDuplicateAsync(
            request.AccountId,
            request.Amount,
            request.Description?.Trim() ?? string.Empty,
            TimeSpan.FromSeconds(2)); // Reduced to 2 seconds for manual transactions
            
        if (recentDuplicate != null)
        {
            return recentDuplicate;
        }

        return null;
    }

    public string GenerateTransactionHash(CreateTransactionCommand request)
    {
        // Create a hash based on key transaction fields for duplicate detection
        // Using full datetime to allow multiple legitimate transactions on the same day
        var hashInput = $"{request.AccountId}|{request.Amount:F2}|{request.TransactionDate:yyyy-MM-dd HH:mm:ss}|{request.Description?.Trim()}";
        
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(hashInput));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
