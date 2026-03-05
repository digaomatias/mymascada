using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Reports.DTOs;

namespace MyMascada.Application.Features.Reports.Queries;

public class GetCategoryTrendsQuery : IRequest<CategoryTrendsResponseDto>
{
    public Guid UserId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public List<int>? CategoryIds { get; set; }
    public int? Limit { get; set; }
}

public class GetCategoryTrendsQueryHandler : IRequestHandler<GetCategoryTrendsQuery, CategoryTrendsResponseDto>
{
    private readonly ITransactionRepository _transactionRepository;

    public GetCategoryTrendsQueryHandler(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    public async Task<CategoryTrendsResponseDto> Handle(GetCategoryTrendsQuery request, CancellationToken cancellationToken)
    {
        // Default to last 12 months if no dates provided
        var endDate = request.EndDate ?? DateTime.UtcNow;
        var startDate = request.StartDate ?? endDate.AddMonths(-11).Date;

        // Normalize to month boundaries
        startDate = new DateTime(startDate.Year, startDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        endDate = new DateTime(endDate.Year, endDate.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddMonths(1).AddTicks(-1);

        // Get all transactions in the date range
        var transactions = await _transactionRepository.GetByDateRangeAsync(
            request.UserId,
            startDate,
            endDate);

        var transactionList = transactions.ToList();

        // Filter to expenses only (negative amounts), non-transfers, with categories
        var expenseTransactions = transactionList
            .Where(t => t.Amount < 0 && !t.TransferId.HasValue && t.Category != null)
            .ToList();

        // If category IDs specified, filter further
        if (request.CategoryIds != null && request.CategoryIds.Any())
        {
            expenseTransactions = expenseTransactions
                .Where(t => request.CategoryIds.Contains(t.Category!.Id))
                .ToList();
        }

        // Group by category and month
        var categoryMonthData = expenseTransactions
            .GroupBy(t => new
            {
                CategoryId = t.Category!.Id,
                CategoryName = t.Category.Name,
                CategoryColor = t.Category.Color,
                Year = t.TransactionDate.Year,
                Month = t.TransactionDate.Month
            })
            .Select(g => new
            {
                g.Key.CategoryId,
                g.Key.CategoryName,
                g.Key.CategoryColor,
                g.Key.Year,
                g.Key.Month,
                Amount = Math.Abs(g.Sum(t => t.Amount)),
                TransactionCount = g.Count()
            })
            .ToList();

        // Calculate total spending per category
        var categoryTotals = categoryMonthData
            .GroupBy(d => new { d.CategoryId, d.CategoryName, d.CategoryColor })
            .Select(g => new
            {
                g.Key.CategoryId,
                g.Key.CategoryName,
                g.Key.CategoryColor,
                TotalSpent = g.Sum(d => d.Amount),
                MonthlyData = g.ToList()
            })
            .OrderByDescending(c => c.TotalSpent)
            .ToList();

        // Apply limit if specified
        if (request.Limit.HasValue && request.Limit.Value > 0)
        {
            categoryTotals = categoryTotals.Take(request.Limit.Value).ToList();
        }

        // Calculate number of months in the period
        var monthCount = ((endDate.Year - startDate.Year) * 12) + endDate.Month - startDate.Month + 1;

        // Build period labels (generate all months in range)
        var periodLabels = new List<(DateTime PeriodStart, string Label)>();
        var currentPeriod = startDate;
        while (currentPeriod <= endDate)
        {
            periodLabels.Add((
                currentPeriod,
                currentPeriod.ToString("MMM ''yy") // "Jan '25"
            ));
            currentPeriod = currentPeriod.AddMonths(1);
        }

        // Build category trend DTOs
        var categories = new List<CategoryTrendDto>();
        foreach (var cat in categoryTotals)
        {
            var periods = new List<PeriodAmountDto>();
            foreach (var period in periodLabels)
            {
                var monthData = cat.MonthlyData.FirstOrDefault(d =>
                    d.Year == period.PeriodStart.Year && d.Month == period.PeriodStart.Month);

                periods.Add(new PeriodAmountDto
                {
                    PeriodStart = period.PeriodStart,
                    PeriodLabel = period.Label,
                    Amount = monthData?.Amount ?? 0,
                    TransactionCount = monthData?.TransactionCount ?? 0
                });
            }

            // Compute 3-month rolling average trend
            var orderedPeriods = periods.OrderBy(p => p.PeriodStart).ToList();
            var periodCount = orderedPeriods.Count;

            decimal trendPercentage = 0;
            string trend = "stable";

            if (periodCount >= 6)
            {
                var last3 = orderedPeriods.Skip(periodCount - 3).Take(3);
                var prev3 = orderedPeriods.Skip(periodCount - 6).Take(3);
                var last3Avg = last3.Average(p => p.Amount);
                var prev3Avg = prev3.Average(p => p.Amount);

                if (prev3Avg > 0)
                {
                    trendPercentage = ((last3Avg - prev3Avg) / prev3Avg) * 100;
                }

                trend = trendPercentage > 5 ? "up" : trendPercentage < -5 ? "down" : "stable";
            }

            // Find highest and lowest spending months
            var periodsWithSpending = periods.Where(p => p.Amount > 0).ToList();
            PeriodAmountDto? highestMonth = periodsWithSpending.Count > 0
                ? periodsWithSpending.OrderByDescending(p => p.Amount).First()
                : null;
            PeriodAmountDto? lowestMonth = periodsWithSpending.Count > 0
                ? periodsWithSpending.OrderBy(p => p.Amount).First()
                : null;

            categories.Add(new CategoryTrendDto
            {
                CategoryId = cat.CategoryId,
                CategoryName = cat.CategoryName,
                CategoryColor = cat.CategoryColor,
                TotalSpent = cat.TotalSpent,
                AverageMonthlySpent = monthCount > 0 ? cat.TotalSpent / monthCount : 0,
                Periods = periods,
                Trend = trend,
                TrendPercentage = Math.Round(trendPercentage, 2),
                HighestMonth = highestMonth,
                LowestMonth = lowestMonth
            });
        }

        // Build period summaries (total spending per month across all categories)
        var periodSummaries = periodLabels.Select(period =>
        {
            var monthTotal = categoryMonthData
                .Where(d => d.Year == period.PeriodStart.Year && d.Month == period.PeriodStart.Month)
                .Sum(d => d.Amount);

            var monthTransactionCount = categoryMonthData
                .Where(d => d.Year == period.PeriodStart.Year && d.Month == period.PeriodStart.Month)
                .Sum(d => d.TransactionCount);

            return new TrendPeriodSummaryDto
            {
                PeriodStart = period.PeriodStart,
                PeriodLabel = period.Label,
                TotalSpent = monthTotal,
                TransactionCount = monthTransactionCount
            };
        }).ToList();

        var totalSpending = categories.Sum(c => c.TotalSpent);
        var avgMonthlySpending = monthCount > 0 ? totalSpending / monthCount : 0;

        return new CategoryTrendsResponseDto
        {
            StartDate = startDate,
            EndDate = endDate,
            Categories = categories,
            PeriodSummaries = periodSummaries,
            TotalSpending = totalSpending,
            AvgMonthlySpending = avgMonthlySpending
        };
    }
}
