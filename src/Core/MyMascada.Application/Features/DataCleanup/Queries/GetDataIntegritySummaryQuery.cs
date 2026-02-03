using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.DataCleanup.DTOs;

namespace MyMascada.Application.Features.DataCleanup.Queries;

/// <summary>
/// Query to get a summary of data integrity issues
/// </summary>
public class GetDataIntegritySummaryQuery : IRequest<DataIntegritySummaryDto>
{
    public Guid UserId { get; set; }
}

/// <summary>
/// Handler for data integrity summary query
/// </summary>
public class GetDataIntegritySummaryQueryHandler : IRequestHandler<GetDataIntegritySummaryQuery, DataIntegritySummaryDto>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountRepository _accountRepository;

    public GetDataIntegritySummaryQueryHandler(
        ITransactionRepository transactionRepository,
        IAccountRepository accountRepository)
    {
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
    }

    public async Task<DataIntegritySummaryDto> Handle(GetDataIntegritySummaryQuery request, CancellationToken cancellationToken)
    {
        var orphanedTransactionCount = await _transactionRepository.GetOrphanedTransactionCountAsync(request.UserId);
        var orphanedAccounts = await _accountRepository.GetSoftDeletedAccountsWithTransactionsAsync(request.UserId);
        var orphanedTransactions = await _transactionRepository.GetOrphanedTransactionsAsync(request.UserId);

        var result = new DataIntegritySummaryDto
        {
            OrphanedTransactionCount = orphanedTransactionCount,
            OrphanedAccountCount = orphanedAccounts.Count(),
            TotalAffectedTransactions = orphanedTransactionCount,
            TotalAffectedAmount = orphanedTransactions.Sum(t => Math.Abs(t.Amount)),
            LastAnalysis = DateTime.UtcNow
        };

        // Add specific issues
        if (orphanedTransactionCount > 0)
        {
            result.Issues.Add(new DataIntegrityIssue
            {
                Type = "OrphanedTransactions",
                Description = $"{orphanedTransactionCount} transactions reference deleted accounts",
                Count = orphanedTransactionCount,
                Severity = orphanedTransactionCount > 50 ? "Critical" : orphanedTransactionCount > 10 ? "High" : "Medium",
                Details = new Dictionary<string, object>
                {
                    ["TotalAmount"] = orphanedTransactions.Sum(t => Math.Abs(t.Amount)),
                    ["AffectedAccounts"] = orphanedAccounts.Count()
                }
            });
        }

        if (orphanedAccounts.Any())
        {
            result.Issues.Add(new DataIntegrityIssue
            {
                Type = "SoftDeletedAccountsWithTransactions",
                Description = $"{orphanedAccounts.Count()} deleted accounts still have active transactions",
                Count = orphanedAccounts.Count(),
                Severity = "High",
                Details = new Dictionary<string, object>
                {
                    ["AccountNames"] = orphanedAccounts.Select(a => a.Name).ToList()
                }
            });
        }

        return result;
    }
}