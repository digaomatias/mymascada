using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.DataCleanup.DTOs;
using MyMascada.Application.Features.Transactions.DTOs;

namespace MyMascada.Application.Features.DataCleanup.Queries;

/// <summary>
/// Query to analyze orphaned transactions and soft-deleted accounts
/// </summary>
public class GetOrphanedDataAnalysisQuery : IRequest<OrphanedDataAnalysisDto>
{
    public Guid UserId { get; set; }
    public bool IncludeTransactionDetails { get; set; } = true;
}

/// <summary>
/// Handler for orphaned data analysis query
/// </summary>
public class GetOrphanedDataAnalysisQueryHandler : IRequestHandler<GetOrphanedDataAnalysisQuery, OrphanedDataAnalysisDto>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountRepository _accountRepository;

    public GetOrphanedDataAnalysisQueryHandler(
        ITransactionRepository transactionRepository,
        IAccountRepository accountRepository)
    {
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
    }

    public async Task<OrphanedDataAnalysisDto> Handle(GetOrphanedDataAnalysisQuery request, CancellationToken cancellationToken)
    {
        var orphanedTransactions = await _transactionRepository.GetOrphanedTransactionsAsync(request.UserId);
        var orphanedAccounts = await _accountRepository.GetSoftDeletedAccountsWithTransactionsAsync(request.UserId);

        var result = new OrphanedDataAnalysisDto
        {
            OrphanedTransactionCount = orphanedTransactions.Count(),
            AnalysisTimestamp = DateTime.UtcNow
        };

        // Group transactions by account
        var transactionsByAccount = orphanedTransactions.GroupBy(t => t.AccountId);

        foreach (var group in transactionsByAccount)
        {
            var account = orphanedAccounts.FirstOrDefault(a => a.Id == group.Key);
            if (account != null)
            {
                var orphanedAccountDto = new OrphanedAccountDto
                {
                    Id = account.Id,
                    Name = account.Name,
                    Institution = account.Institution,
                    LastFourDigits = account.LastFourDigits,
                    DeletedAt = account.DeletedAt,
                    TransactionCount = group.Count(),
                    TotalTransactionAmount = group.Sum(t => t.Amount)
                };

                if (request.IncludeTransactionDetails)
                {
                    orphanedAccountDto.Transactions = group.Select(t => new TransactionDto
                    {
                        Id = t.Id,
                        Amount = t.Amount,
                        TransactionDate = t.TransactionDate,
                        Description = t.Description,
                        UserDescription = t.UserDescription,
                        Status = t.Status,
                        CategoryId = t.CategoryId,
                        CategoryName = t.Category?.Name,
                        AccountId = t.AccountId,
                        AccountName = t.Account.Name,
                        IsReviewed = t.IsReviewed,
                        Type = t.Type
                    }).ToList();

                    result.OrphanedTransactions.AddRange(orphanedAccountDto.Transactions);
                }

                result.OrphanedAccounts.Add(orphanedAccountDto);
            }
        }

        return result;
    }
}