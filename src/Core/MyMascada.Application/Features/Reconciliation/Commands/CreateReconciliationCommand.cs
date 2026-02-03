using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Reconciliation.DTOs;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Reconciliation.Commands;

public record CreateReconciliationCommand : IRequest<ReconciliationDto>
{
    public Guid UserId { get; init; }
    public int AccountId { get; init; }
    public DateTime StatementEndDate { get; init; }
    public decimal StatementEndBalance { get; init; }
    public string? Notes { get; init; }
}

public class CreateReconciliationCommandHandler : IRequestHandler<CreateReconciliationCommand, ReconciliationDto>
{
    private readonly IReconciliationRepository _reconciliationRepository;
    private readonly IReconciliationAuditLogRepository _auditLogRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;

    public CreateReconciliationCommandHandler(
        IReconciliationRepository reconciliationRepository,
        IReconciliationAuditLogRepository auditLogRepository,
        IAccountRepository accountRepository,
        ITransactionRepository transactionRepository)
    {
        _reconciliationRepository = reconciliationRepository;
        _auditLogRepository = auditLogRepository;
        _accountRepository = accountRepository;
        _transactionRepository = transactionRepository;
    }

    public async Task<ReconciliationDto> Handle(CreateReconciliationCommand request, CancellationToken cancellationToken)
    {
        // Verify account exists and belongs to user
        var account = await _accountRepository.GetByIdAsync(request.AccountId, request.UserId);
        if (account == null)
            throw new ArgumentException($"Account with ID {request.AccountId} not found or does not belong to user");

        // Calculate the current balance from transactions
        var calculatedBalance = await _transactionRepository.GetAccountBalanceAsync(request.AccountId, request.UserId);

        // Create the reconciliation
        var reconciliation = new Domain.Entities.Reconciliation
        {
            AccountId = request.AccountId,
            ReconciliationDate = DateTime.UtcNow,
            StatementEndDate = request.StatementEndDate,
            StatementEndBalance = request.StatementEndBalance,
            CalculatedBalance = calculatedBalance,
            Status = ReconciliationStatus.InProgress,
            CreatedByUserId = request.UserId,
            Notes = request.Notes,
            CreatedBy = request.UserId.ToString(),
            UpdatedBy = request.UserId.ToString()
        };

        var savedReconciliation = await _reconciliationRepository.AddAsync(reconciliation);

        // Create audit log entry
        var auditLog = new ReconciliationAuditLog
        {
            ReconciliationId = savedReconciliation.Id,
            Action = ReconciliationAction.ReconciliationStarted,
            UserId = request.UserId,
            CreatedBy = request.UserId.ToString(),
            UpdatedBy = request.UserId.ToString()
        };

        auditLog.SetDetails(new
        {
            AccountId = request.AccountId,
            AccountName = account.Name,
            StatementEndDate = request.StatementEndDate,
            StatementEndBalance = request.StatementEndBalance,
            CalculatedBalance = calculatedBalance,
            BalanceDifference = savedReconciliation.BalanceDifference
        });

        await _auditLogRepository.AddAsync(auditLog);

        return new ReconciliationDto
        {
            Id = savedReconciliation.Id,
            AccountId = savedReconciliation.AccountId,
            AccountName = account.Name,
            ReconciliationDate = savedReconciliation.ReconciliationDate,
            StatementEndDate = savedReconciliation.StatementEndDate,
            StatementEndBalance = savedReconciliation.StatementEndBalance,
            CalculatedBalance = savedReconciliation.CalculatedBalance,
            Status = savedReconciliation.Status,
            CreatedByUserId = savedReconciliation.CreatedByUserId,
            CompletedAt = savedReconciliation.CompletedAt,
            Notes = savedReconciliation.Notes,
            BalanceDifference = savedReconciliation.BalanceDifference,
            IsBalanced = savedReconciliation.IsBalanced,
            TotalItemsCount = savedReconciliation.TotalItemsCount,
            MatchedItemsCount = savedReconciliation.MatchedItemsCount,
            MatchedPercentage = savedReconciliation.MatchedPercentage,
            CreatedAt = savedReconciliation.CreatedAt,
            UpdatedAt = savedReconciliation.UpdatedAt
        };
    }
}