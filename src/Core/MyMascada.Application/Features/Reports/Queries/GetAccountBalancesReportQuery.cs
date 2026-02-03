using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Reports.DTOs;

namespace MyMascada.Application.Features.Reports.Queries;

public class GetAccountBalancesReportQuery : IRequest<IEnumerable<AccountBalanceReportDto>>
{
    public Guid UserId { get; set; }
}

public class GetAccountBalancesReportQueryHandler : IRequestHandler<GetAccountBalancesReportQuery, IEnumerable<AccountBalanceReportDto>>
{
    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;

    public GetAccountBalancesReportQueryHandler(
        IAccountRepository accountRepository, 
        ITransactionRepository transactionRepository)
    {
        _accountRepository = accountRepository;
        _transactionRepository = transactionRepository;
    }

    public async Task<IEnumerable<AccountBalanceReportDto>> Handle(GetAccountBalancesReportQuery request, CancellationToken cancellationToken)
    {
        var accounts = await _accountRepository.GetByUserIdAsync(request.UserId);
        var accountBalances = new List<AccountBalanceReportDto>();

        foreach (var account in accounts.Where(a => a.IsActive))
        {
            var transactions = await _transactionRepository.GetByAccountIdAsync(account.Id, request.UserId);
            var transactionList = transactions.ToList();

            accountBalances.Add(new AccountBalanceReportDto
            {
                AccountId = account.Id,
                AccountName = account.Name,
                CurrentBalance = account.CurrentBalance,
                Currency = account.Currency,
                TransactionCount = transactionList.Count,
                LastTransactionDate = transactionList
                    .OrderByDescending(t => t.TransactionDate)
                    .FirstOrDefault()?.TransactionDate
            });
        }

        return accountBalances.OrderBy(a => a.AccountName);
    }
}