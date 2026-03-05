using System.Globalization;
using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Reports.DTOs;
using MyMascada.Domain.Common;

namespace MyMascada.Application.Features.Reports.Queries;

public class GetCashflowHistoryQuery : IRequest<CashflowHistoryDto>
{
    public Guid UserId { get; set; }
    public int Months { get; set; } = 7;
}

public class GetCashflowHistoryQueryHandler : IRequestHandler<GetCashflowHistoryQuery, CashflowHistoryDto>
{
    private readonly ITransactionRepository _transactionRepository;

    public GetCashflowHistoryQueryHandler(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    public async Task<CashflowHistoryDto> Handle(GetCashflowHistoryQuery request, CancellationToken cancellationToken)
    {
        var now = DateTimeProvider.UtcNow;
        var months = Math.Max(1, Math.Min(request.Months, 24));

        // Calculate date range: from (months-1) months ago start to end of current month
        var rangeStartDate = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddMonths(-(months - 1));
        var rangeEndDate = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddMonths(1).AddTicks(-1);

        // Single DB query for the entire range
        var transactions = (await _transactionRepository.GetByDateRangeAsync(
            request.UserId, rangeStartDate, rangeEndDate)).ToList();

        // Group by year, month and calculate income/expenses (excluding transfers)
        var grouped = transactions
            .Where(t => !t.TransferId.HasValue)
            .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
            .ToDictionary(
                g => (g.Key.Year, g.Key.Month),
                g => (
                    Income: g.Where(t => t.Amount > 0).Sum(t => t.Amount),
                    Expenses: Math.Abs(g.Where(t => t.Amount < 0).Sum(t => t.Amount))
                ));

        // Build result for each month in the range (including months with no data)
        var result = new List<CashflowMonthDto>();
        for (var i = 0; i < months; i++)
        {
            var monthDate = rangeStartDate.AddMonths(i);
            var key = (monthDate.Year, monthDate.Month);
            var income = 0m;
            var expenses = 0m;

            if (grouped.TryGetValue(key, out var values))
            {
                income = values.Income;
                expenses = values.Expenses;
            }

            result.Add(new CashflowMonthDto
            {
                Year = monthDate.Year,
                Month = monthDate.Month,
                Label = CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(monthDate.Month),
                Income = income,
                Expenses = expenses,
                Net = income - expenses
            });
        }

        return new CashflowHistoryDto { Months = result };
    }
}
