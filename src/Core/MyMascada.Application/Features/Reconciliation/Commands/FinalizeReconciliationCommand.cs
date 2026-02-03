using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Reconciliation.DTOs;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Reconciliation.Commands;

public record FinalizeReconciliationCommand : IRequest<ReconciliationDto>
{
    public Guid UserId { get; init; }
    public int ReconciliationId { get; init; }
    public string? Notes { get; init; }
    public bool ForceFinalize { get; init; } = false;
}

public class FinalizeReconciliationCommandHandler : IRequestHandler<FinalizeReconciliationCommand, ReconciliationDto>
{
    private readonly IReconciliationRepository _reconciliationRepository;
    private readonly IReconciliationItemRepository _reconciliationItemRepository;
    private readonly IReconciliationAuditLogRepository _auditLogRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;

    public FinalizeReconciliationCommandHandler(
        IReconciliationRepository reconciliationRepository,
        IReconciliationItemRepository reconciliationItemRepository,
        IReconciliationAuditLogRepository auditLogRepository,
        IAccountRepository accountRepository,
        ITransactionRepository transactionRepository)
    {
        _reconciliationRepository = reconciliationRepository;
        _reconciliationItemRepository = reconciliationItemRepository;
        _auditLogRepository = auditLogRepository;
        _accountRepository = accountRepository;
        _transactionRepository = transactionRepository;
    }

    public async Task<ReconciliationDto> Handle(FinalizeReconciliationCommand request, CancellationToken cancellationToken)
    {
        // Verify reconciliation exists and belongs to user
        var reconciliation = await _reconciliationRepository.GetByIdAsync(request.ReconciliationId, request.UserId);
        if (reconciliation == null)
            throw new ArgumentException($"Reconciliation with ID {request.ReconciliationId} not found or does not belong to user");

        // Check if reconciliation is already finalized
        if (reconciliation.Status == ReconciliationStatus.Completed)
            throw new InvalidOperationException("Reconciliation is already completed");

        // Get all reconciliation items
        var allItems = await _reconciliationItemRepository.GetByReconciliationIdAsync(request.ReconciliationId, request.UserId);
        var itemsList = allItems.ToList();

        // Validate reconciliation can be finalized
        var validation = ValidateReconciliationForFinalization(itemsList, request.ForceFinalize);
        if (!validation.CanFinalize)
            throw new InvalidOperationException($"Cannot finalize reconciliation: {validation.Reason}");

        // Calculate final reconciliation statistics
        var stats = CalculateReconciliationStatistics(itemsList);

        // Update reconciliation status
        reconciliation.Status = ReconciliationStatus.Completed;
        reconciliation.CompletedAt = DateTime.UtcNow;
        reconciliation.UpdatedBy = request.UserId.ToString();

        // Add notes if provided
        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            // In a real implementation, you might have a Notes field on the Reconciliation entity
            // For now, we'll add it to the audit log
        }

        await _reconciliationRepository.UpdateAsync(reconciliation);

        // Update account's last reconciled tracking fields
        var account = await _accountRepository.GetByIdAsync(reconciliation.AccountId, request.UserId);
        if (account != null)
        {
            account.LastReconciledDate = reconciliation.StatementEndDate;
            account.LastReconciledBalance = reconciliation.StatementEndBalance;
            account.UpdatedAt = DateTime.UtcNow;
            await _accountRepository.UpdateAsync(account);
        }

        // Mark all matched transactions as Reconciled
        var matchedItems = itemsList.Where(i =>
            i.ItemType == ReconciliationItemType.Matched &&
            i.TransactionId.HasValue).ToList();

        var transactionsMarkedReconciled = 0;
        foreach (var item in matchedItems)
        {
            var transaction = await _transactionRepository.GetByIdAsync(item.TransactionId!.Value, request.UserId);
            if (transaction != null && transaction.Status != TransactionStatus.Reconciled)
            {
                transaction.Status = TransactionStatus.Reconciled;
                transaction.UpdatedAt = DateTime.UtcNow;
                await _transactionRepository.UpdateAsync(transaction);
                transactionsMarkedReconciled++;
            }
        }

        // Create audit log entry
        var auditLog = new Domain.Entities.ReconciliationAuditLog
        {
            ReconciliationId = reconciliation.Id,
            Action = ReconciliationAction.ReconciliationCompleted,
            Details = System.Text.Json.JsonSerializer.Serialize(new
            {
                TotalItems = stats.TotalItems,
                MatchedItems = stats.MatchedItems,
                UnmatchedBankItems = stats.UnmatchedBankItems,
                UnmatchedSystemItems = stats.UnmatchedSystemItems,
                MatchPercentage = stats.MatchPercentage,
                TransactionsMarkedReconciled = transactionsMarkedReconciled,
                Notes = request.Notes,
                ForceFinalized = request.ForceFinalize
            }),
            Timestamp = DateTime.UtcNow,
            UserId = request.UserId
        };

        await _auditLogRepository.AddAsync(auditLog);

        // Return updated reconciliation as DTO
        return new ReconciliationDto
        {
            Id = reconciliation.Id,
            AccountId = reconciliation.AccountId,
            StatementEndDate = reconciliation.StatementEndDate,
            StatementEndBalance = reconciliation.StatementEndBalance,
            Status = reconciliation.Status,
            CompletedAt = reconciliation.CompletedAt,
            CreatedAt = reconciliation.CreatedAt,
            UpdatedAt = reconciliation.UpdatedAt
        };
    }

    private (bool CanFinalize, string Reason) ValidateReconciliationForFinalization(
        List<Domain.Entities.ReconciliationItem> items, bool forceFinalize)
    {
        if (forceFinalize)
            return (true, "Force finalized by user");

        var unmatchedBankItems = items.Count(i => i.ItemType == ReconciliationItemType.UnmatchedBank);
        var unmatchedSystemItems = items.Count(i => i.ItemType == ReconciliationItemType.UnmatchedApp);

        // Allow finalization if no unmatched items
        if (unmatchedBankItems == 0 && unmatchedSystemItems == 0)
            return (true, "All transactions matched");

        // Allow finalization if unmatched items are below threshold (e.g., 5% of total)
        var totalItems = items.Count;
        var unmatchedItems = unmatchedBankItems + unmatchedSystemItems;
        var unmatchedPercentage = totalItems > 0 ? (decimal)unmatchedItems / totalItems * 100 : 0;

        if (unmatchedPercentage <= 5m) // 5% threshold
            return (true, "Unmatched items within acceptable threshold (≤5%)");

        return (false, $"Too many unmatched items ({unmatchedPercentage:F1}%). Use force finalize to override.");
    }

    private ReconciliationStatistics CalculateReconciliationStatistics(List<Domain.Entities.ReconciliationItem> items)
    {
        var totalItems = items.Count;
        var matchedItems = items.Count(i => i.ItemType == ReconciliationItemType.Matched);
        var unmatchedBankItems = items.Count(i => i.ItemType == ReconciliationItemType.UnmatchedBank);
        var unmatchedSystemItems = items.Count(i => i.ItemType == ReconciliationItemType.UnmatchedApp);

        return new ReconciliationStatistics
        {
            TotalItems = totalItems,
            MatchedItems = matchedItems,
            UnmatchedBankItems = unmatchedBankItems,
            UnmatchedSystemItems = unmatchedSystemItems,
            MatchPercentage = totalItems > 0 ? (decimal)matchedItems / totalItems * 100 : 0
        };
    }

    private class ReconciliationStatistics
    {
        public int TotalItems { get; set; }
        public int MatchedItems { get; set; }
        public int UnmatchedBankItems { get; set; }
        public int UnmatchedSystemItems { get; set; }
        public decimal MatchPercentage { get; set; }
    }
}