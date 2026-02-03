using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Reconciliation.DTOs;
using MyMascada.Application.Features.Reconciliation.Services;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Reconciliation.Queries;

public record GetReconciliationDetailsQuery : IRequest<ReconciliationDetailsDto>
{
    public int ReconciliationId { get; init; }
    public Guid UserId { get; init; }
    public string? SearchTerm { get; init; }
    public ReconciliationItemType? FilterByType { get; init; }
    public MatchMethod? FilterByMatchMethod { get; init; }
    public decimal? MinAmount { get; init; }
    public decimal? MaxAmount { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
}

public class GetReconciliationDetailsQueryHandler : IRequestHandler<GetReconciliationDetailsQuery, ReconciliationDetailsDto>
{
    private readonly IReconciliationRepository _reconciliationRepository;
    private readonly IReconciliationItemRepository _reconciliationItemRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IMatchConfidenceCalculator _matchConfidenceCalculator;

    public GetReconciliationDetailsQueryHandler(
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

    public async Task<ReconciliationDetailsDto> Handle(GetReconciliationDetailsQuery request, CancellationToken cancellationToken)
    {
        // Verify reconciliation exists and belongs to user
        var reconciliation = await _reconciliationRepository.GetByIdAsync(request.ReconciliationId, request.UserId);
        if (reconciliation == null)
            throw new ArgumentException($"Reconciliation with ID {request.ReconciliationId} not found or does not belong to user");

        // Get all reconciliation items
        var allItems = await _reconciliationItemRepository.GetByReconciliationIdAsync(request.ReconciliationId, request.UserId);
        var itemsList = allItems.ToList();

        // Get transaction details for matched items
        var transactionIds = itemsList
            .Where(i => i.TransactionId.HasValue)
            .Select(i => i.TransactionId!.Value)
            .ToList();

        var transactions = transactionIds.Any() 
            ? await _transactionRepository.GetTransactionsByIdsAsync(transactionIds, request.UserId, cancellationToken)
            : new List<Domain.Entities.Transaction>();

        var transactionLookup = transactions.ToDictionary(t => t.Id);

        // Convert to DTOs with detailed information and enhanced match analysis
        var itemDtos = itemsList.Select(item => 
        {
            var bankTransaction = ParseBankTransactionFromJson(item.BankReferenceData);
            var systemTransaction = item.TransactionId.HasValue && transactionLookup.TryGetValue(item.TransactionId.Value, out var transaction)
                ? transaction : null;
            
            // Calculate enhanced match analysis for matched items
            MatchAnalysisDto? matchAnalysis = null;
            if (systemTransaction != null && bankTransaction != null && item.ItemType == ReconciliationItemType.Matched)
            {
                matchAnalysis = _matchConfidenceCalculator.AnalyzeMatch(systemTransaction, bankTransaction);
            }
            
            return new ReconciliationItemDetailDto
            {
                Id = item.Id,
                ReconciliationId = item.ReconciliationId,
                TransactionId = item.TransactionId,
                ItemType = item.ItemType,
                MatchConfidence = item.MatchConfidence,
                MatchMethod = item.MatchMethod,
                BankTransaction = bankTransaction,
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
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            };
        }).ToList();

        // Apply filters
        var filteredItems = ApplyFilters(itemDtos, request);

        // Categorize items
        var exactMatches = filteredItems.Where(i => i.ItemType == ReconciliationItemType.Matched && 
                                                    i.MatchConfidence >= 0.95m).ToList();
        var fuzzyMatches = filteredItems.Where(i => i.ItemType == ReconciliationItemType.Matched && 
                                                    i.MatchConfidence < 0.95m).ToList();
        var unmatchedBank = filteredItems.Where(i => i.ItemType == ReconciliationItemType.UnmatchedBank).ToList();
        var unmatchedSystem = filteredItems.Where(i => i.ItemType == ReconciliationItemType.UnmatchedApp).ToList();

        // Calculate summary statistics
        var summary = new ReconciliationDetailsSummaryDto
        {
            TotalItems = itemsList.Count,
            ExactMatches = exactMatches.Count,
            FuzzyMatches = fuzzyMatches.Count,
            UnmatchedBank = unmatchedBank.Count,
            UnmatchedSystem = unmatchedSystem.Count,
            MatchPercentage = itemsList.Count > 0 ? 
                ((decimal)(exactMatches.Count + fuzzyMatches.Count) / itemsList.Count) * 100 : 0
        };

        return new ReconciliationDetailsDto
        {
            ReconciliationId = request.ReconciliationId,
            Summary = summary,
            ExactMatches = exactMatches,
            FuzzyMatches = fuzzyMatches,
            UnmatchedBankTransactions = unmatchedBank,
            UnmatchedSystemTransactions = unmatchedSystem
        };
    }

    private List<ReconciliationItemDetailDto> ApplyFilters(List<ReconciliationItemDetailDto> items, GetReconciliationDetailsQuery request)
    {
        var filtered = items.AsEnumerable();

        // Filter by type
        if (request.FilterByType.HasValue)
            filtered = filtered.Where(i => i.ItemType == request.FilterByType.Value);

        // Filter by match method
        if (request.FilterByMatchMethod.HasValue)
            filtered = filtered.Where(i => i.MatchMethod == request.FilterByMatchMethod.Value);

        // Filter by amount range
        if (request.MinAmount.HasValue || request.MaxAmount.HasValue)
        {
            filtered = filtered.Where(i =>
            {
                var amount = GetItemAmount(i);
                return (!request.MinAmount.HasValue || Math.Abs(amount) >= request.MinAmount.Value) &&
                       (!request.MaxAmount.HasValue || Math.Abs(amount) <= request.MaxAmount.Value);
            });
        }

        // Filter by date range
        if (request.StartDate.HasValue || request.EndDate.HasValue)
        {
            filtered = filtered.Where(i =>
            {
                var date = GetItemDate(i);
                return (!request.StartDate.HasValue || date >= request.StartDate.Value) &&
                       (!request.EndDate.HasValue || date <= request.EndDate.Value);
            });
        }

        // Filter by search term
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchLower = request.SearchTerm.ToLowerInvariant();
            filtered = filtered.Where(i =>
            {
                var description = GetItemDescription(i);
                return description.ToLowerInvariant().Contains(searchLower);
            });
        }

        return filtered.ToList();
    }

    private decimal GetItemAmount(ReconciliationItemDetailDto item)
    {
        return item.SystemTransaction?.Amount ?? item.BankTransaction?.Amount ?? 0;
    }

    private DateTime GetItemDate(ReconciliationItemDetailDto item)
    {
        return item.SystemTransaction?.TransactionDate ?? item.BankTransaction?.TransactionDate ?? DateTime.MinValue;
    }

    private string GetItemDescription(ReconciliationItemDetailDto item)
    {
        return item.SystemTransaction?.Description ?? item.BankTransaction?.Description ?? string.Empty;
    }

    private BankTransactionDto? ParseBankTransactionFromJson(string? bankReferenceData)
    {
        if (string.IsNullOrWhiteSpace(bankReferenceData))
            return null;

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<BankTransactionDto>(bankReferenceData);
        }
        catch
        {
            return null;
        }
    }
}