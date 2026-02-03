using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Reports.DTOs;

namespace MyMascada.Application.Features.Reports.Queries;

public class GetMonthlySummaryQuery : IRequest<MonthlySummaryDto>
{
    public Guid UserId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
}

public class GetMonthlySummaryQueryHandler : IRequestHandler<GetMonthlySummaryQuery, MonthlySummaryDto>
{
    private readonly ITransactionRepository _transactionRepository;

    public GetMonthlySummaryQueryHandler(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    public async Task<MonthlySummaryDto> Handle(GetMonthlySummaryQuery request, CancellationToken cancellationToken)
    {
        // Get month boundaries (ensure UTC for PostgreSQL compatibility)
        var monthStart = new DateTime(request.Year, request.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1).AddTicks(-1); // End of the last day of the month

        // Get transactions for the month
        var monthlyTransactions = await _transactionRepository.GetByDateRangeAsync(
            request.UserId,
            monthStart,
            monthEnd);
        
        var transactionList = monthlyTransactions.ToList();

        // Calculate totals (excluding transfers)
        var totalIncome = transactionList
            .Where(t => t.Amount > 0 && !t.TransferId.HasValue)
            .Sum(t => t.Amount);

        var totalExpenses = Math.Abs(transactionList
            .Where(t => t.Amount < 0 && !t.TransferId.HasValue)
            .Sum(t => t.Amount));

        var netAmount = totalIncome - totalExpenses;

        // Get top categories by spending (expenses only, excluding transfers)
        var topCategories = transactionList
            .Where(t => t.Amount < 0 && t.Category != null && !t.TransferId.HasValue)
            .GroupBy(t => new { t.Category!.Id, t.Category.Name, t.Category.Color })
            .Select(g => new CategorySpendingDto
            {
                CategoryId = g.Key.Id,
                CategoryName = g.Key.Name,
                CategoryColor = g.Key.Color,
                Amount = Math.Abs(g.Sum(t => t.Amount)),
                TransactionCount = g.Count()
            })
            .OrderByDescending(c => c.Amount)
            .Take(5)
            .ToList();

        // Calculate percentages for categories
        foreach (var category in topCategories)
        {
            category.Percentage = totalExpenses > 0 ? (category.Amount / totalExpenses) * 100 : 0;
        }

        return new MonthlySummaryDto
        {
            Year = request.Year,
            Month = request.Month,
            MonthName = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(request.Month),
            TotalIncome = totalIncome,
            TotalExpenses = totalExpenses,
            NetAmount = netAmount,
            TransactionCount = transactionList.Count,
            TopCategories = topCategories
        };
    }
}
