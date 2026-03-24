using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Reports.DTOs;
using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;

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
        var now = DateTimeProvider.UtcNow;

        // Get user's accounts
        var userAccounts = (await _accountRepository.GetByUserIdAsync(request.UserId)).ToList();

        // Get real-time balances (initial balance + transaction sums)
        var accountBalances = await _transactionRepository.GetAccountBalancesAsync(request.UserId);

        decimal GetBalance(Domain.Entities.Account a) => accountBalances.GetValueOrDefault(a.Id, a.CurrentBalance);

        // Calculate total balance from real-time balances
        var totalBalance = userAccounts.Sum(GetBalance);

        // Net worth breakdown: CreditCard(3) + Loan(5) = liabilities, rest = assets
        var totalAssets = userAccounts
            .Where(a => a.Type != AccountType.CreditCard && a.Type != AccountType.Loan)
            .Sum(GetBalance);
        var totalLiabilities = Math.Abs(userAccounts
            .Where(a => a.Type == AccountType.CreditCard || a.Type == AccountType.Loan)
            .Sum(GetBalance));
        var netWorth = totalAssets - totalLiabilities;

        // Get current month boundaries
        var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var currentMonthEnd = currentMonthStart.AddMonths(1).AddTicks(-1);

        // Get current month transactions
        var currentMonthTransactions = (await _transactionRepository.GetByDateRangeAsync(
            request.UserId, currentMonthStart, currentMonthEnd)).ToList();

        // Check if current month has non-transfer transactions (for fallback)
        var nonTransferCount = currentMonthTransactions.Count(t => !t.TransferId.HasValue);
        var isUsingFallbackMonth = nonTransferCount == 0;

        DateTime displayMonthStart;
        DateTime displayMonthEnd;
        int displayMonth;
        int displayYear;

        if (isUsingFallbackMonth)
        {
            // Use previous month
            var prevMonthDate = currentMonthStart.AddMonths(-1);
            displayMonthStart = new DateTime(prevMonthDate.Year, prevMonthDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            displayMonthEnd = displayMonthStart.AddMonths(1).AddTicks(-1);
            displayMonth = prevMonthDate.Month;
            displayYear = prevMonthDate.Year;
        }
        else
        {
            displayMonthStart = currentMonthStart;
            displayMonthEnd = currentMonthEnd;
            displayMonth = now.Month;
            displayYear = now.Year;
        }

        // Get display month transactions (may be current or previous month)
        List<Domain.Entities.Transaction> displayMonthTransactions;
        if (isUsingFallbackMonth)
        {
            displayMonthTransactions = (await _transactionRepository.GetByDateRangeAsync(
                request.UserId, displayMonthStart, displayMonthEnd)).ToList();
        }
        else
        {
            displayMonthTransactions = currentMonthTransactions;
        }

        // Calculate monthly income and expenses for display month (excluding transfers)
        var monthlyIncome = displayMonthTransactions
            .Where(t => t.Amount > 0 && !t.TransferId.HasValue)
            .Sum(t => t.Amount);

        var monthlyExpenses = Math.Abs(displayMonthTransactions
            .Where(t => t.Amount < 0 && !t.TransferId.HasValue)
            .Sum(t => t.Amount));

        // 3-month rolling average runway calculation
        var (avgMonthlyIncome, avgMonthlyExpenses) = await CalculateRollingAveragesAsync(
            request.UserId, now);

        var netSaved = avgMonthlyIncome - avgMonthlyExpenses;
        var runwayMonths = avgMonthlyExpenses > 0
            ? Math.Max(0m, Math.Round(totalBalance / avgMonthlyExpenses, 1))
            : 0m;
        var savingsRate = avgMonthlyIncome > 0
            ? Math.Round((netSaved / avgMonthlyIncome) * 100, 0)
            : 0m;

        // Get recent transactions (last 5)
        var recentTransactions = await _transactionRepository.GetRecentAsync(request.UserId, 5);

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
            RecentTransactions = recentTransactionDtos,
            RunwayMonths = runwayMonths,
            SavingsRate = savingsRate,
            NetSaved = netSaved,
            AvgMonthlyIncome = avgMonthlyIncome,
            AvgMonthlyExpenses = avgMonthlyExpenses,
            TotalAssets = totalAssets,
            TotalLiabilities = totalLiabilities,
            NetWorth = netWorth,
            IsUsingFallbackMonth = isUsingFallbackMonth,
            DisplayMonth = displayMonth,
            DisplayYear = displayYear
        };
    }

    private async Task<(decimal avgIncome, decimal avgExpenses)> CalculateRollingAveragesAsync(
        Guid userId, DateTime now)
    {
        // Load last 3 complete months in a single query
        var threeMonthsAgoDate = now.AddMonths(-3);
        var rangeStart = new DateTime(threeMonthsAgoDate.Year, threeMonthsAgoDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthDate = now.AddMonths(-1);
        var rangeEnd = new DateTime(lastMonthDate.Year, lastMonthDate.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1).AddTicks(-1);

        var allTransactions = (await _transactionRepository.GetByDateRangeAsync(
            userId, rangeStart, rangeEnd)).ToList();

        var monthlySummaries = new List<(decimal income, decimal expenses)>();

        for (var i = 1; i <= 3; i++)
        {
            var monthDate = now.AddMonths(-i);
            var monthStart = new DateTime(monthDate.Year, monthDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEnd = monthStart.AddMonths(1).AddTicks(-1);

            var transactions = allTransactions
                .Where(t => t.TransactionDate >= monthStart && t.TransactionDate <= monthEnd)
                .ToList();

            var income = transactions
                .Where(t => t.Amount > 0 && !t.TransferId.HasValue)
                .Sum(t => t.Amount);
            var expenses = Math.Abs(transactions
                .Where(t => t.Amount < 0 && !t.TransferId.HasValue)
                .Sum(t => t.Amount));

            monthlySummaries.Add((income, expenses));
        }

        // Filter to "valid" months where totalIncome > 0 OR totalExpenses > 0
        var validMonths = monthlySummaries
            .Where(m => m.income > 0 || m.expenses > 0)
            .ToList();

        // If fewer than 3 valid months AND dayOfMonth > 5, project current partial month
        if (validMonths.Count < 3 && now.Day > 5)
        {
            var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var currentMonthEnd = currentMonthStart.AddMonths(1).AddTicks(-1);
            var currentTransactions = (await _transactionRepository.GetByDateRangeAsync(
                userId, currentMonthStart, currentMonthEnd)).ToList();

            var currentIncome = currentTransactions
                .Where(t => t.Amount > 0 && !t.TransferId.HasValue)
                .Sum(t => t.Amount);
            var currentExpenses = Math.Abs(currentTransactions
                .Where(t => t.Amount < 0 && !t.TransferId.HasValue)
                .Sum(t => t.Amount));

            if (currentIncome > 0 || currentExpenses > 0)
            {
                // Project to full month
                var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
                var dayOfMonth = now.Day;
                var projectionFactor = (decimal)daysInMonth / dayOfMonth;

                validMonths.Add((currentIncome * projectionFactor, currentExpenses * projectionFactor));
            }
        }

        if (validMonths.Count == 0)
        {
            return (0m, 0m);
        }

        var avgIncome = validMonths.Average(m => m.income);
        var avgExpenses = validMonths.Average(m => m.expenses);

        return (avgIncome, avgExpenses);
    }
}
