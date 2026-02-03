using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Reports.DTOs;

namespace MyMascada.Application.Features.Reports.Queries;

public class GetDashboardSummaryQuery : IRequest<DashboardSummaryDto>
{
    public Guid UserId { get; set; }
}

public class GetDashboardSummaryQueryHandler : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;

    public GetDashboardSummaryQueryHandler(
        IAccountRepository accountRepository, 
        ITransactionRepository transactionRepository)
    {
        _accountRepository = accountRepository;
        _transactionRepository = transactionRepository;
    }

    public async Task<DashboardSummaryDto> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        // Get user's accounts
        var userAccounts = await _accountRepository.GetByUserIdAsync(request.UserId);
        
        // Calculate total balance from accounts
        var totalBalance = userAccounts.Sum(a => a.CurrentBalance);

        // Get current month boundaries (ensure UTC for PostgreSQL compatibility)
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1).AddTicks(-1); // End of the last day of the month

        // Get recent transactions (last 5) 
        var recentTransactions = await _transactionRepository.GetRecentAsync(request.UserId, 5);

        // Get monthly transactions for calculations
        var monthlyTransactions = await _transactionRepository.GetByDateRangeAsync(
            request.UserId,
            monthStart,
            monthEnd);

        // Calculate monthly income and expenses (excluding transfers)
        var monthlyIncome = monthlyTransactions
            .Where(t => t.Amount > 0 && !t.TransferId.HasValue)
            .Sum(t => t.Amount);

        var monthlyExpenses = Math.Abs(monthlyTransactions
            .Where(t => t.Amount < 0 && !t.TransferId.HasValue)
            .Sum(t => t.Amount));

        // Get total transaction count
        var totalTransactionCount = await _transactionRepository.GetCountByUserIdAsync(request.UserId);

        // Map recent transactions to DTOs
        var recentTransactionDtos = recentTransactions.Select(t => new RecentTransactionDto
        {
            Id = t.Id,
            Amount = t.Amount,
            TransactionDate = t.TransactionDate,
            Description = t.Description,
            UserDescription = t.UserDescription,
            AccountName = t.Account?.Name ?? "Unknown Account",
            CategoryName = t.Category?.Name,
            CategoryColor = t.Category?.Color
        }).ToList();

        return new DashboardSummaryDto
        {
            TotalBalance = totalBalance,
            MonthlyIncome = monthlyIncome,
            MonthlyExpenses = monthlyExpenses,
            TransactionCount = totalTransactionCount,
            RecentTransactions = recentTransactionDtos
        };
    }
}
