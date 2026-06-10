using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.RecurringTransactions.DTOs;
using MyMascada.Application.Features.RecurringTransactions.Mappings;
using MyMascada.Domain.Common;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.RecurringTransactions.Commands;

public class CreateRecurringTransactionCommand : IRequest<RecurringTransactionDto>, IRecurringTransactionBaseCommand
{
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

public class CreateRecurringTransactionCommandHandler
    : IRequestHandler<CreateRecurringTransactionCommand, RecurringTransactionDto>
{
    private readonly IRecurringTransactionRepository _recurringTransactionRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IAccountAccessService _accountAccessService;

    public CreateRecurringTransactionCommandHandler(
        IRecurringTransactionRepository recurringTransactionRepository,
        IAccountRepository accountRepository,
        ICategoryRepository categoryRepository,
        IAccountAccessService accountAccessService)
    {
        _recurringTransactionRepository = recurringTransactionRepository;
        _accountRepository = accountRepository;
        _categoryRepository = categoryRepository;
        _accountAccessService = accountAccessService;
    }

    public async Task<RecurringTransactionDto> Handle(
        CreateRecurringTransactionCommand request,
        CancellationToken cancellationToken)
    {
        // Validate account exists and belongs to user
        var account = await _accountRepository.GetByIdAsync(request.AccountId, request.UserId);
        if (account == null)
        {
            throw new ArgumentException($"Account with ID {request.AccountId} not found or does not belong to user");
        }

        // Auto-create schedules materialize real transactions through the standard
        // pipeline, which requires modify access (owner or Manager share). Reject
        // viewer-only accounts up front instead of letting the nightly job fail on
        // every run. Reminder-only schedules are fine with view access.
        if (request.AutoCreate
            && !await _accountAccessService.CanModifyAccountAsync(request.UserId, request.AccountId))
        {
            throw new UnauthorizedAccessException(
                "You do not have permission to auto-create transactions on this account.");
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

        var recurringTransaction = new RecurringTransaction
        {
            UserId = request.UserId,
            AccountId = request.AccountId,
            CategoryId = request.CategoryId,
            Description = request.Description.Trim(),
            Amount = request.Amount,
            Frequency = request.Frequency,
            CustomIntervalDays = request.Frequency == RecurrenceFrequency.Custom ? request.CustomIntervalDays : null,
            StartDate = DateTimeProvider.ToUtc(request.StartDate.Date),
            EndDate = request.EndDate.HasValue ? DateTimeProvider.ToUtc(request.EndDate.Value.Date) : null,
            NextDueDate = DateTimeProvider.ToUtc(request.StartDate.Date),
            AutoCreate = request.AutoCreate,
            IsActive = true,
            Notes = request.Notes,
            CreatedAt = DateTimeProvider.UtcNow,
            UpdatedAt = DateTimeProvider.UtcNow
        };

        // Fast-forward schedules that start in the past so the job does not
        // retroactively fire occurrences from before the schedule was created.
        var today = DateTimeProvider.UtcNow.Date;
        if (recurringTransaction.NextDueDate.Date < today)
        {
            recurringTransaction.AdvanceNextDueDate(today.AddDays(-1));
        }

        var created = await _recurringTransactionRepository.CreateAsync(recurringTransaction, cancellationToken);
        return RecurringTransactionMapper.ToDto(created);
    }
}
