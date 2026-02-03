using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Reconciliation.DTOs;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Reconciliation.Queries;

public record GetReconciliationItemsQuery : IRequest<IEnumerable<ReconciliationItemDto>>
{
    public int ReconciliationId { get; init; }
    public Guid UserId { get; init; }
    public ReconciliationItemType? ItemType { get; init; }
    public decimal? MinConfidence { get; init; }
    public MatchMethod? MatchMethod { get; init; }
}

public class GetReconciliationItemsQueryHandler : IRequestHandler<GetReconciliationItemsQuery, IEnumerable<ReconciliationItemDto>>
{
    private readonly IReconciliationItemRepository _reconciliationItemRepository;
    private readonly ITransactionRepository _transactionRepository;

    public GetReconciliationItemsQueryHandler(
        IReconciliationItemRepository reconciliationItemRepository,
        ITransactionRepository transactionRepository)
    {
        _reconciliationItemRepository = reconciliationItemRepository;
        _transactionRepository = transactionRepository;
    }

    public async Task<IEnumerable<ReconciliationItemDto>> Handle(GetReconciliationItemsQuery request, CancellationToken cancellationToken)
    {
        var items = await _reconciliationItemRepository.GetByReconciliationIdAsync(request.ReconciliationId, request.UserId);

        // Filter items if requested
        if (request.ItemType.HasValue)
            items = items.Where(i => i.ItemType == request.ItemType.Value);

        if (request.MinConfidence.HasValue)
            items = items.Where(i => i.MatchConfidence >= request.MinConfidence.Value);

        if (request.MatchMethod.HasValue)
            items = items.Where(i => i.MatchMethod == request.MatchMethod.Value);

        var itemsList = items.ToList();

        // Get transaction details for matched items
        var transactionIds = itemsList
            .Where(i => i.TransactionId.HasValue)
            .Select(i => i.TransactionId!.Value)
            .ToList();

        var transactions = transactionIds.Any() 
            ? await _transactionRepository.GetTransactionsByIdsAsync(transactionIds, request.UserId, cancellationToken)
            : new List<Domain.Entities.Transaction>();

        var transactionLookup = transactions.ToDictionary(t => t.Id);

        return itemsList.Select(item => new ReconciliationItemDto
        {
            Id = item.Id,
            ReconciliationId = item.ReconciliationId,
            TransactionId = item.TransactionId,
            ItemType = item.ItemType,
            MatchConfidence = item.MatchConfidence,
            MatchMethod = item.MatchMethod,
            BankReferenceData = item.BankReferenceData,
            Transaction = item.TransactionId.HasValue && transactionLookup.TryGetValue(item.TransactionId.Value, out var transaction)
                ? new TransactionDetailsDto
                {
                    Id = transaction.Id,
                    Amount = transaction.Amount,
                    Description = transaction.Description,
                    TransactionDate = transaction.TransactionDate,
                    CategoryName = transaction.Category?.Name,
                    Status = transaction.Status
                }
                : null,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        });
    }
}