using MyMascada.Application.Features.Reconciliation.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Reconciliation.Services;

public interface ITransactionMatchingService
{
    Task<MatchingResultDto> MatchTransactionsAsync(
        TransactionMatchRequest request, 
        IEnumerable<Transaction> appTransactions);
    
    Task<MatchedPairDto?> FindBestMatchAsync(
        BankTransactionDto bankTransaction, 
        IEnumerable<Transaction> appTransactions,
        decimal toleranceAmount = 0.01m,
        bool useDescriptionMatching = true,
        bool useDateRangeMatching = true,
        int dateRangeToleranceDays = 2);
    
    decimal CalculateMatchConfidence(
        BankTransactionDto bankTransaction, 
        Transaction appTransaction,
        bool useDescriptionMatching = true,
        bool useDateRangeMatching = true,
        int dateRangeToleranceDays = 2);
}