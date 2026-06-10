using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.RecurringTransactions.DTOs;
using MyMascada.Application.Features.RecurringTransactions.Mappings;
using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.RecurringTransactions.Commands;

public class UpdateRecurringTransactionCommand : IRequest<RecurringTransactionDto>, IRecurringTransactionBaseCommand
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public int AccountId { get; set; }
    public int? CategoryId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public RecurrenceFrequency Frequency { get; set; }
    public int? CustomIntervalDays { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool AutoCreate { get; set; }
    public string? Notes { get; set; }
}

public class UpdateRecurringTransactionCommandHandler
    : IRequestHandler<UpdateRecurringTransactionCommand, RecurringTransactionDto>
{
    private readonly IRecurringTransactionRepository _recurringTransactionRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ICategoryRepository _categoryRepository;

    public UpdateRecurringTransactionCommandHandler(
        IRecurringTransactionRepository recurringTransactionRepository,
        IAccountRepository accountRepository,
        ICategoryRepository categoryRepository)
    {
        _recurringTransactionRepository = recurringTransactionRepository;
        _accountRepository = accountRepository;
        _categoryRepository = categoryRepository;
    }

    public async Task<RecurringTransactionDto> Handle(
        UpdateRecurringTransactionCommand request,
        CancellationToken cancellationToken)
    {
        var recurringTransaction = await _recurringTransactionRepository.GetByIdAsync(
            request.Id, request.UserId, cancellationToken);
        if (recurringTransaction == null)
        {
            throw new ArgumentException($"Recurring transaction with ID {request.Id} not found");
        }

        // Validate account exists and belongs to user
        var account = await _accountRepository.GetByIdAsync(request.AccountId, request.UserId);
        if (account == null)
        {
            throw new ArgumentException($"Account with ID {request.AccountId} not found or does not belong to user");
        }

        // Validate category if provided
        if (request.CategoryId.HasValue)
        {
            var categoryExists = await _categoryRepository.ExistsAsync(request.CategoryId.Value, request.UserId);
            if (!categoryExists)
            {
                throw new ArgumentException($"Category with ID {request.CategoryId} not found or does not belong to user");
            }
        }

        var newStartDate = DateTimeProvider.ToUtc(request.StartDate.Date);
        var scheduleChanged = recurringTransaction.StartDate.Date != newStartDate.Date
                              || recurringTransaction.Frequency != request.Frequency
                              || recurringTransaction.CustomIntervalDays != request.CustomIntervalDays;

        recurringTransaction.AccountId = request.AccountId;
        recurringTransaction.CategoryId = request.CategoryId;
        recurringTransaction.Description = request.Description.Trim();
        recurringTransaction.Amount = request.Amount;
        recurringTransaction.Frequency = request.Frequency;
        recurringTransaction.CustomIntervalDays =
            request.Frequency == RecurrenceFrequency.Custom ? request.CustomIntervalDays : null;
        recurringTransaction.StartDate = newStartDate;
        recurringTransaction.EndDate = request.EndDate.HasValue
            ? DateTimeProvider.ToUtc(request.EndDate.Value.Date)
            : null;
        recurringTransaction.AutoCreate = request.AutoCreate;
        recurringTransaction.Notes = request.Notes;

        if (scheduleChanged)
        {
            // Recompute the next due date from the new schedule, fast-forwarding
            // past dates so the job does not retroactively fire old occurrences.
            recurringTransaction.NextDueDate = newStartDate;
            var today = DateTimeProvider.UtcNow.Date;
            if (recurringTransaction.NextDueDate.Date < today)
            {
                recurringTransaction.AdvanceNextDueDate(today.AddDays(-1));
            }
        }

        // A shrunk EndDate may invalidate the current NextDueDate
        if (recurringTransaction.EndDate.HasValue
            && recurringTransaction.NextDueDate.Date > recurringTransaction.EndDate.Value.Date)
        {
            recurringTransaction.IsActive = false;
        }

        var updated = await _recurringTransactionRepository.UpdateAsync(recurringTransaction, cancellationToken);
        return RecurringTransactionMapper.ToDto(updated);
    }
}
