using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.RecurringTransactions.DTOs;
using MyMascada.Domain.Common;

namespace MyMascada.Application.Features.RecurringTransactions.Queries;

/// <summary>
/// Materializes the upcoming schedule for the user's active recurring transactions
/// (user-created bills — distinct from heuristic upcoming-bills detection).
/// </summary>
public class GetUpcomingRecurringTransactionsQuery : IRequest<List<UpcomingRecurringTransactionDto>>
{
    public Guid UserId { get; set; }
    public int DaysAhead { get; set; } = 30;
}

public class GetUpcomingRecurringTransactionsQueryHandler
    : IRequestHandler<GetUpcomingRecurringTransactionsQuery, List<UpcomingRecurringTransactionDto>>
{
    private readonly IRecurringTransactionRepository _recurringTransactionRepository;

    public GetUpcomingRecurringTransactionsQueryHandler(
        IRecurringTransactionRepository recurringTransactionRepository)
    {
        _recurringTransactionRepository = recurringTransactionRepository;
    }

    public async Task<List<UpcomingRecurringTransactionDto>> Handle(
        GetUpcomingRecurringTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        var daysAhead = Math.Clamp(request.DaysAhead, 1, 366);
        var fromDate = DateTimeProvider.UtcNow.Date;
        var toDate = fromDate.AddDays(daysAhead);

        var activeRecurringTransactions = await _recurringTransactionRepository.GetActiveByUserIdAsync(
            request.UserId, cancellationToken);

        var upcoming = new List<UpcomingRecurringTransactionDto>();

        foreach (var recurringTransaction in activeRecurringTransactions)
        {
            foreach (var dueDate in recurringTransaction.GetScheduledDates(fromDate, toDate))
            {
                upcoming.Add(new UpcomingRecurringTransactionDto
                {
                    RecurringTransactionId = recurringTransaction.Id,
                    Description = recurringTransaction.Description,
                    Amount = recurringTransaction.Amount,
                    AccountId = recurringTransaction.AccountId,
                    AccountName = recurringTransaction.Account?.Name ?? string.Empty,
                    CategoryId = recurringTransaction.CategoryId,
                    CategoryName = recurringTransaction.Category?.Name,
                    DueDate = dueDate,
                    AutoCreate = recurringTransaction.AutoCreate
                });
            }
        }

        return upcoming
            .OrderBy(u => u.DueDate)
            .ThenBy(u => u.Description)
            .ToList();
    }
}
