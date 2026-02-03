using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Reconciliation.DTOs;
using MyMascada.Application.Features.Reconciliation.Services;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Reconciliation.Commands;

public record ManualMatchTransactionCommand : IRequest<ReconciliationItemDetailDto>
{
    public Guid UserId { get; init; }
    public int ReconciliationId { get; init; }
    public int? SystemTransactionId { get; init; }
    public BankTransactionDto? BankTransaction { get; init; }
    public string? Notes { get; init; }
}

public class ManualMatchTransactionCommandHandler : IRequestHandler<ManualMatchTransactionCommand, ReconciliationItemDetailDto>
{
    private readonly IReconciliationRepository _reconciliationRepository;
    private readonly IReconciliationItemRepository _reconciliationItemRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IMatchConfidenceCalculator _matchConfidenceCalculator;

    public ManualMatchTransactionCommandHandler(
        IReconciliationRepository reconciliationRepository,
        IReconciliationItemRepository reconciliationItemRepository,
        ITransactionRepository transactionRepository,
        IMatchConfidenceCalculator matchConfidenceCalculator)
    {
        _reconciliationRepository = reconciliationRepository;
        _reconciliationItemRepository = reconciliationItemRepository;
        _transactionRepository = transactionRepository;
        _matchConfidenceCalculator = matchConfidenceCalculator;
    }

    public async Task<ReconciliationItemDetailDto> Handle(ManualMatchTransactionCommand request, CancellationToken cancellationToken)
    {
        // Verify reconciliation exists and belongs to user
        var reconciliation = await _reconciliationRepository.GetByIdAsync(request.ReconciliationId, request.UserId);
        if (reconciliation == null)
            throw new ArgumentException($"Reconciliation with ID {request.ReconciliationId} not found or does not belong to user");

        // Verify transaction exists if provided
        Transaction? systemTransaction = null;
        if (request.SystemTransactionId.HasValue)
        {
            systemTransaction = await _transactionRepository.GetByIdAsync(request.SystemTransactionId.Value, request.UserId);
            if (systemTransaction == null)
                throw new ArgumentException($"Transaction with ID {request.SystemTransactionId} not found or does not belong to user");
        }

        // Calculate match confidence if both transactions are available
        decimal? matchConfidence = null;
        MatchAnalysisDto? matchAnalysis = null;
        if (systemTransaction != null && request.BankTransaction != null)
        {
            matchConfidence = _matchConfidenceCalculator.CalculateMatchConfidence(systemTransaction, request.BankTransaction);
            matchAnalysis = _matchConfidenceCalculator.AnalyzeMatch(systemTransaction, request.BankTransaction);
        }

        // Create new reconciliation item
        var reconciliationItem = new ReconciliationItem
        {
            ReconciliationId = request.ReconciliationId,
            TransactionId = request.SystemTransactionId,
            ItemType = DetermineItemType(request.SystemTransactionId, request.BankTransaction),
            MatchConfidence = matchConfidence,
            MatchMethod = MatchMethod.Manual,
            BankReferenceData = request.BankTransaction != null 
                ? System.Text.Json.JsonSerializer.Serialize(request.BankTransaction) 
                : null,
            CreatedBy = request.UserId.ToString(),
            UpdatedBy = request.UserId.ToString()
        };

        await _reconciliationItemRepository.AddAsync(reconciliationItem);

        // Return the created item as DTO
        return new ReconciliationItemDetailDto
        {
            Id = reconciliationItem.Id,
            ReconciliationId = reconciliationItem.ReconciliationId,
            TransactionId = reconciliationItem.TransactionId,
            ItemType = reconciliationItem.ItemType,
            MatchConfidence = reconciliationItem.MatchConfidence,
            MatchMethod = reconciliationItem.MatchMethod,
            BankTransaction = request.BankTransaction,
            SystemTransaction = systemTransaction != null
                ? new TransactionDetailsDto
                {
                    Id = systemTransaction.Id,
                    Amount = systemTransaction.Amount,
                    Description = systemTransaction.Description,
                    TransactionDate = systemTransaction.TransactionDate,
                    CategoryName = systemTransaction.Category?.Name,
                    Status = systemTransaction.Status
                }
                : null,
            MatchAnalysis = matchAnalysis,
            CreatedAt = reconciliationItem.CreatedAt,
            UpdatedAt = reconciliationItem.UpdatedAt
        };
    }

    private ReconciliationItemType DetermineItemType(int? systemTransactionId, BankTransactionDto? bankTransaction)
    {
        if (systemTransactionId.HasValue && bankTransaction != null)
            return ReconciliationItemType.Matched;
        
        if (systemTransactionId.HasValue)
            return ReconciliationItemType.UnmatchedApp;
        
        if (bankTransaction != null)
            return ReconciliationItemType.UnmatchedBank;

        throw new ArgumentException("Either system transaction ID or bank transaction must be provided");
    }
}