using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Reconciliation.DTOs;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Reconciliation.Commands;

public record UpdateReconciliationCommand : IRequest<ReconciliationDto>
{
    public Guid UserId { get; init; }
    public int ReconciliationId { get; init; }
    public DateTime? StatementEndDate { get; init; }
    public decimal? StatementEndBalance { get; init; }
    public ReconciliationStatus? Status { get; init; }
    public string? Notes { get; init; }
}

public class UpdateReconciliationCommandHandler : IRequestHandler<UpdateReconciliationCommand, ReconciliationDto>
{
    private readonly IReconciliationRepository _reconciliationRepository;
    private readonly IReconciliationAuditLogRepository _auditLogRepository;
    private readonly IAccountRepository _accountRepository;

    public UpdateReconciliationCommandHandler(
        IReconciliationRepository reconciliationRepository,
        IReconciliationAuditLogRepository auditLogRepository,
        IAccountRepository accountRepository)
    {
        _reconciliationRepository = reconciliationRepository;
        _auditLogRepository = auditLogRepository;
        _accountRepository = accountRepository;
    }

    public async Task<ReconciliationDto> Handle(UpdateReconciliationCommand request, CancellationToken cancellationToken)
    {
        var reconciliation = await _reconciliationRepository.GetByIdAsync(request.ReconciliationId, request.UserId);
        if (reconciliation == null)
            throw new ArgumentException($"Reconciliation with ID {request.ReconciliationId} not found or does not belong to user");

        var account = await _accountRepository.GetByIdAsync(reconciliation.AccountId, request.UserId);
        if (account == null)
            throw new ArgumentException($"Account with ID {reconciliation.AccountId} not found");

        // Store old values for audit
        var oldValues = new
        {
            StatementEndDate = reconciliation.StatementEndDate,
            StatementEndBalance = reconciliation.StatementEndBalance,
            Status = reconciliation.Status,
            Notes = reconciliation.Notes
        };

        // Update fields
        if (request.StatementEndDate.HasValue)
            reconciliation.StatementEndDate = request.StatementEndDate.Value;

        if (request.StatementEndBalance.HasValue)
            reconciliation.StatementEndBalance = request.StatementEndBalance.Value;

        if (request.Status.HasValue)
        {
            reconciliation.Status = request.Status.Value;
            
            // Set completion timestamp if marking as completed
            if (request.Status.Value == ReconciliationStatus.Completed)
                reconciliation.CompletedAt = DateTime.UtcNow;
            else if (reconciliation.CompletedAt.HasValue)
                reconciliation.CompletedAt = null; // Clear if status changed from completed
        }

        if (request.Notes != null)
            reconciliation.Notes = request.Notes;

        reconciliation.UpdatedBy = request.UserId.ToString();

        await _reconciliationRepository.UpdateAsync(reconciliation);

        // Create audit log entry
        var newValues = new
        {
            StatementEndDate = reconciliation.StatementEndDate,
            StatementEndBalance = reconciliation.StatementEndBalance,
            Status = reconciliation.Status,
            Notes = reconciliation.Notes
        };

        var auditAction = request.Status switch
        {
            ReconciliationStatus.Completed => ReconciliationAction.ReconciliationCompleted,
            ReconciliationStatus.Cancelled => ReconciliationAction.ReconciliationCancelled,
            null => ReconciliationAction.ReconciliationStarted, // No status change
            _ => ReconciliationAction.ReconciliationStarted // Generic update
        };

        var auditLog = new ReconciliationAuditLog
        {
            ReconciliationId = reconciliation.Id,
            Action = auditAction,
            UserId = request.UserId,
            CreatedBy = request.UserId.ToString(),
            UpdatedBy = request.UserId.ToString()
        };

        auditLog.SetOldValues(oldValues);
        auditLog.SetNewValues(newValues);

        await _auditLogRepository.AddAsync(auditLog);

        return new ReconciliationDto
        {
            Id = reconciliation.Id,
            AccountId = reconciliation.AccountId,
            AccountName = account.Name,
            ReconciliationDate = reconciliation.ReconciliationDate,
            StatementEndDate = reconciliation.StatementEndDate,
            StatementEndBalance = reconciliation.StatementEndBalance,
            CalculatedBalance = reconciliation.CalculatedBalance,
            Status = reconciliation.Status,
            CreatedByUserId = reconciliation.CreatedByUserId,
            CompletedAt = reconciliation.CompletedAt,
            Notes = reconciliation.Notes,
            BalanceDifference = reconciliation.BalanceDifference,
            IsBalanced = reconciliation.IsBalanced,
            TotalItemsCount = reconciliation.TotalItemsCount,
            MatchedItemsCount = reconciliation.MatchedItemsCount,
            MatchedPercentage = reconciliation.MatchedPercentage,
            CreatedAt = reconciliation.CreatedAt,
            UpdatedAt = reconciliation.UpdatedAt
        };
    }
}