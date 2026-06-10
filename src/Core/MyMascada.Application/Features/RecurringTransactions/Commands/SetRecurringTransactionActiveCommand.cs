using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.RecurringTransactions.DTOs;
using MyMascada.Application.Features.RecurringTransactions.Mappings;
using MyMascada.Domain.Common;

namespace MyMascada.Application.Features.RecurringTransactions.Commands;

/// <summary>
/// Pauses (IsActive = false) or resumes (IsActive = true) a recurring transaction.
/// Resuming fast-forwards NextDueDate so occurrences missed while paused are not fired.
/// </summary>
public class SetRecurringTransactionActiveCommand : IRequest<RecurringTransactionDto>
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public bool IsActive { get; set; }
}

public class SetRecurringTransactionActiveCommandHandler
    : IRequestHandler<SetRecurringTransactionActiveCommand, RecurringTransactionDto>
{
    private readonly IRecurringTransactionRepository _recurringTransactionRepository;

    public SetRecurringTransactionActiveCommandHandler(IRecurringTransactionRepository recurringTransactionRepository)
    {
        _recurringTransactionRepository = recurringTransactionRepository;
    }

    public async Task<RecurringTransactionDto> Handle(
        SetRecurringTransactionActiveCommand request,
        CancellationToken cancellationToken)
    {
        var recurringTransaction = await _recurringTransactionRepository.GetByIdAsync(
            request.Id, request.UserId, cancellationToken);
        if (recurringTransaction == null)
        {
            throw new KeyNotFoundException($"Recurring transaction with ID {request.Id} not found");
        }

        if (request.IsActive)
        {
            recurringTransaction.Resume(DateTimeProvider.UtcNow.Date);
        }
        else
        {
            recurringTransaction.Pause();
        }

        var updated = await _recurringTransactionRepository.UpdateAsync(recurringTransaction, cancellationToken);
        return RecurringTransactionMapper.ToDto(updated);
    }
}
