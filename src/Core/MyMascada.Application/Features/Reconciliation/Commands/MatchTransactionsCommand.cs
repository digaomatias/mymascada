using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Reconciliation.DTOs;
using MyMascada.Application.Features.Reconciliation.Services;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Reconciliation.Commands;

public record MatchTransactionsCommand : IRequest<MatchingResultDto>
{
    public Guid UserId { get; init; }
    public int ReconciliationId { get; init; }
    public IEnumerable<BankTransactionDto> BankTransactions { get; init; } = new List<BankTransactionDto>();
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public decimal ToleranceAmount { get; init; } = 0.01m;
    public bool UseDescriptionMatching { get; init; } = true;
    public bool UseDateRangeMatching { get; init; } = true;
    public int DateRangeToleranceDays { get; init; } = 2;
}

public class MatchTransactionsCommandHandler : IRequestHandler<MatchTransactionsCommand, MatchingResultDto>
{
    private readonly IReconciliationRepository _reconciliationRepository;
    private readonly IReconciliationItemRepository _reconciliationItemRepository;
    private readonly IReconciliationAuditLogRepository _auditLogRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ITransactionMatchingService _matchingService;

    public MatchTransactionsCommandHandler(
        IReconciliationRepository reconciliationRepository,
        IReconciliationItemRepository reconciliationItemRepository,
        IReconciliationAuditLogRepository auditLogRepository,
        ITransactionRepository transactionRepository,
        ITransactionMatchingService matchingService)
    {
        _reconciliationRepository = reconciliationRepository;
        _reconciliationItemRepository = reconciliationItemRepository;
        _auditLogRepository = auditLogRepository;
        _transactionRepository = transactionRepository;
        _matchingService = matchingService;
    }

    public async Task<MatchingResultDto> Handle(MatchTransactionsCommand request, CancellationToken cancellationToken)
    {
        // Verify reconciliation exists and belongs to user
        var reconciliation = await _reconciliationRepository.GetByIdAsync(request.ReconciliationId, request.UserId);
        if (reconciliation == null)
            throw new ArgumentException($"Reconciliation with ID {request.ReconciliationId} not found or does not belong to user");

        // Get app transactions for the account within the date range
        // Note: This includes both reviewed and unreviewed transactions for comprehensive reconciliation
        // Excludes already-reconciled transactions to avoid redundant re-reconciliation
        var startDate = request.StartDate ?? reconciliation.StatementEndDate.AddDays(-30);
        var endDate = request.EndDate ?? reconciliation.StatementEndDate;

        var accountTransactions = await _transactionRepository.GetByDateRangeAsync(
            request.UserId,
            reconciliation.AccountId,
            startDate,
            endDate,
            excludeReconciled: true);

        // Include unreviewed transactions in reconciliation to ensure comprehensive matching
        var unreviewedCount = accountTransactions.Count(t => !t.IsReviewed);
        var reviewedCount = accountTransactions.Count(t => t.IsReviewed);
        
        // Note: Total includes both reviewed ({reviewedCount}) and unreviewed ({unreviewedCount}) transactions

        // Clear existing reconciliation items
        await _reconciliationItemRepository.DeleteByReconciliationIdAsync(request.ReconciliationId, request.UserId);

        // Perform matching
        var matchingRequest = new TransactionMatchRequest
        {
            ReconciliationId = request.ReconciliationId,
            BankTransactions = request.BankTransactions,
            StartDate = startDate,
            EndDate = endDate,
            ToleranceAmount = request.ToleranceAmount,
            UseDescriptionMatching = request.UseDescriptionMatching,
            UseDateRangeMatching = request.UseDateRangeMatching,
            DateRangeToleranceDays = request.DateRangeToleranceDays
        };

        var matchingResult = await _matchingService.MatchTransactionsAsync(matchingRequest, accountTransactions);

        // Create reconciliation items from matching results
        var reconciliationItems = new List<ReconciliationItem>();

        // Add matched pairs
        foreach (var matchedPair in matchingResult.MatchedPairs)
        {
            var item = new ReconciliationItem
            {
                ReconciliationId = request.ReconciliationId,
                TransactionId = matchedPair.AppTransaction.Id,
                ItemType = ReconciliationItemType.Matched,
                MatchConfidence = matchedPair.MatchConfidence,
                MatchMethod = matchedPair.MatchMethod,
                CreatedBy = request.UserId.ToString(),
                UpdatedBy = request.UserId.ToString()
            };

            item.SetBankReferenceData(matchedPair.BankTransaction);
            reconciliationItems.Add(item);
        }

        // Add unmatched app transactions
        foreach (var unmatchedApp in matchingResult.UnmatchedAppTransactions)
        {
            var item = new ReconciliationItem
            {
                ReconciliationId = request.ReconciliationId,
                TransactionId = unmatchedApp.Id,
                ItemType = ReconciliationItemType.UnmatchedApp,
                CreatedBy = request.UserId.ToString(),
                UpdatedBy = request.UserId.ToString()
            };

            reconciliationItems.Add(item);
        }

        // Add unmatched bank transactions
        foreach (var unmatchedBank in matchingResult.UnmatchedBankTransactions)
        {
            var item = new ReconciliationItem
            {
                ReconciliationId = request.ReconciliationId,
                ItemType = ReconciliationItemType.UnmatchedBank,
                CreatedBy = request.UserId.ToString(),
                UpdatedBy = request.UserId.ToString()
            };

            item.SetBankReferenceData(unmatchedBank);
            reconciliationItems.Add(item);
        }

        // Save all reconciliation items
        await _reconciliationItemRepository.AddRangeAsync(reconciliationItems);

        // Create audit log entry
        var auditLog = new ReconciliationAuditLog
        {
            ReconciliationId = request.ReconciliationId,
            Action = ReconciliationAction.BankStatementImported,
            UserId = request.UserId,
            CreatedBy = request.UserId.ToString(),
            UpdatedBy = request.UserId.ToString()
        };

        auditLog.SetDetails(new
        {
            BankTransactionCount = request.BankTransactions.Count(),
            AppTransactionCount = accountTransactions.Count(),
            ExactMatches = matchingResult.ExactMatches,
            FuzzyMatches = matchingResult.FuzzyMatches,
            UnmatchedBank = matchingResult.UnmatchedBank,
            UnmatchedApp = matchingResult.UnmatchedApp,
            OverallMatchPercentage = matchingResult.OverallMatchPercentage
        });

        await _auditLogRepository.AddAsync(auditLog);

        return matchingResult;
    }
}
