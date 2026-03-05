using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Reports.DTOs;

namespace MyMascada.Application.Features.Reports.Queries;

public class GetAnalyticsSummaryQuery : IRequest<AnalyticsSummaryDto>
{
    public Guid UserId { get; set; }
    public string Period { get; set; } = "year";
    public int? Year { get; set; }
    public int? Month { get; set; }
}

public class GetAnalyticsSummaryQueryHandler : IRequestHandler<GetAnalyticsSummaryQuery, AnalyticsSummaryDto>
{
    private readonly ITransactionRepository _transactionRepository;

    public GetAnalyticsSummaryQueryHandler(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    public async Task<AnalyticsSummaryDto> Handle(GetAnalyticsSummaryQuery request, CancellationToken cancellationToken)
    {
        var (startDate, endDate) = CalculateDateRange(request);

        var transactions = await _transactionRepository.GetByDateRangeAsync(
            request.UserId,
            startDate,
            endDate);

        var transactionList = transactions
            .Where(t => !t.TransferId.HasValue)
            .ToList();

        // Group by year/month
        var monthlyGroups = transactionList
            .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
            .Select(g =>
            {
                var income = g.Where(t => t.Amount > 0).Sum(t => t.Amount);
                var expenses = g.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount));
                var net = income - expenses;
                var savingsRate = net > 0 && income > 0 ? (net / income) * 100 : 0;

                return new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Income = income,
                    Expenses = expenses,
                    Net = net,
                    SavingsRate = savingsRate
                };
            })
            .OrderBy(g => g.Year)
            .ThenBy(g => g.Month)
            .ToList();

        var totalIncome = monthlyGroups.Sum(m => m.Income);
        var totalExpenses = monthlyGroups.Sum(m => m.Expenses);
        var netAmount = totalIncome - totalExpenses;
        var monthCount = monthlyGroups.Count;
        var overallSavingsRate = netAmount > 0 && totalIncome > 0 ? (netAmount / totalIncome) * 100 : 0;

        // Build monthly trends
        var monthlyTrends = monthlyGroups.Select(m => new MonthlyTrendDto
        {
            Year = m.Year,
            Month = m.Month,
            Label = new DateTime(m.Year, m.Month, 1).ToString("MMM yyyy"),
            Income = m.Income,
            Expenses = m.Expenses,
            Net = m.Net,
            SavingsRate = m.SavingsRate
        }).ToList();

        // Best and worst months
        MonthHighlightDto? bestMonth = null;
        MonthHighlightDto? worstMonth = null;

        if (monthlyGroups.Count > 0)
        {
            var best = monthlyGroups.OrderByDescending(m => m.Net).First();
            bestMonth = new MonthHighlightDto
            {
                Year = best.Year,
                Month = best.Month,
                Label = new DateTime(best.Year, best.Month, 1).ToString("MMM yyyy"),
                NetAmount = best.Net
            };

            var worst = monthlyGroups.OrderBy(m => m.Net).First();
            worstMonth = new MonthHighlightDto
            {
                Year = worst.Year,
                Month = worst.Month,
                Label = new DateTime(worst.Year, worst.Month, 1).ToString("MMM yyyy"),
                NetAmount = worst.Net
            };
        }

        // Yearly comparisons (last 3 years from the data)
        var yearlyComparisons = monthlyGroups
            .GroupBy(m => m.Year)
            .OrderByDescending(g => g.Key)
            .Take(3)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var yearIncome = g.Sum(m => m.Income);
                var yearExpenses = g.Sum(m => m.Expenses);
                var yearNet = yearIncome - yearExpenses;
                var yearSavingsRate = yearNet > 0 && yearIncome > 0 ? (yearNet / yearIncome) * 100 : 0;

                return new YearlyComparisonDto
                {
                    Year = g.Key,
                    TotalIncome = yearIncome,
                    TotalExpenses = yearExpenses,
                    NetAmount = yearNet,
                    SavingsRate = yearSavingsRate,
                    MonthCount = g.Count()
                };
            })
            .ToList();

        return new AnalyticsSummaryDto
        {
            TotalIncome = totalIncome,
            TotalExpenses = totalExpenses,
            AvgMonthlyIncome = monthCount > 0 ? totalIncome / monthCount : 0,
            AvgMonthlyExpenses = monthCount > 0 ? totalExpenses / monthCount : 0,
            NetAmount = netAmount,
            SavingsRate = overallSavingsRate,
            MonthCount = monthCount,
            BestMonth = bestMonth,
            WorstMonth = worstMonth,
            MonthlyTrends = monthlyTrends,
            YearlyComparisons = yearlyComparisons
        };
    }

    private static (DateTime startDate, DateTime endDate) CalculateDateRange(GetAnalyticsSummaryQuery request)
    {
        var now = DateTime.UtcNow;

        switch (request.Period.ToLowerInvariant())
        {
            case "month":
            {
                var year = request.Year ?? now.Year;
                var month = request.Month ?? now.Month;
                var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
                var end = start.AddMonths(1).AddTicks(-1);
                return (start, end);
            }
            case "quarter":
            {
                var year = request.Year ?? now.Year;
                var month = request.Month ?? now.Month;
                int quarter;
                if (month <= 3) quarter = 1;
                else if (month <= 6) quarter = 2;
                else if (month <= 9) quarter = 3;
                else quarter = 4;

                var startMonth = ((quarter - 1) * 3) + 1;
                var start = new DateTime(year, startMonth, 1, 0, 0, 0, DateTimeKind.Utc);
                var end = start.AddMonths(3).AddTicks(-1);
                return (start, end);
            }
            case "year":
            {
                var year = request.Year ?? now.Year;
                var start = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var end = new DateTime(year, 12, 31, 23, 59, 59, DateTimeKind.Utc);
                return (start, end);
            }
            case "all":
            default:
            {
                var start = DateTime.MinValue;
                var end = now;
                return (start, end);
            }
        }
    }
}
