using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Reconciliation.Commands;

public record UnlinkTransactionCommand : IRequest<bool>
{
    public Guid UserId { get; init; }
    public int ReconciliationItemId { get; init; }
}

public class UnlinkTransactionCommandHandler : IRequestHandler<UnlinkTransactionCommand, bool>
{
    private readonly IReconciliationItemRepository _reconciliationItemRepository;
    private readonly IReconciliationRepository _reconciliationRepository;

    public UnlinkTransactionCommandHandler(
        IReconciliationItemRepository reconciliationItemRepository,
        IReconciliationRepository reconciliationRepository)
    {
        _reconciliationItemRepository = reconciliationItemRepository;
        _reconciliationRepository = reconciliationRepository;
    }

    public async Task<bool> Handle(UnlinkTransactionCommand request, CancellationToken cancellationToken)
    {
        // Find the existing reconciliation item
        var existingItem = await _reconciliationItemRepository.GetByIdAsync(request.ReconciliationItemId, request.UserId);
        if (existingItem == null)
            throw new ArgumentException($"Reconciliation item with ID {request.ReconciliationItemId} not found");

        // Verify the reconciliation belongs to the user
        var reconciliation = await _reconciliationRepository.GetByIdAsync(existingItem.ReconciliationId, request.UserId);
        if (reconciliation == null)
            throw new ArgumentException($"Reconciliation does not belong to user");

        // Only allow unlinking of matched items
        if (existingItem.ItemType != ReconciliationItemType.Matched)
            throw new InvalidOperationException("Can only unlink matched transactions");

        // Parse bank transaction data to preserve it
        var bankTransaction = ParseBankTransactionFromJson(existingItem.BankReferenceData);

        // Delete the existing matched item
        await _reconciliationItemRepository.DeleteAsync(existingItem);

        // Create separate unmatched items if both system and bank transactions exist
        if (existingItem.TransactionId.HasValue)
        {
            // Create unmatched system transaction item
            var unmatchedSystemItem = new Domain.Entities.ReconciliationItem
            {
                ReconciliationId = existingItem.ReconciliationId,
                TransactionId = existingItem.TransactionId,
                ItemType = ReconciliationItemType.UnmatchedApp,
                MatchConfidence = null,
                MatchMethod = null,
                BankReferenceData = null,
                CreatedBy = request.UserId.ToString(),
                UpdatedBy = request.UserId.ToString()
            };

            await _reconciliationItemRepository.AddAsync(unmatchedSystemItem);
        }

        if (bankTransaction != null)
        {
            // Create unmatched bank transaction item
            var unmatchedBankItem = new Domain.Entities.ReconciliationItem
            {
                ReconciliationId = existingItem.ReconciliationId,
                TransactionId = null,
                ItemType = ReconciliationItemType.UnmatchedBank,
                MatchConfidence = null,
                MatchMethod = null,
                BankReferenceData = existingItem.BankReferenceData,
                CreatedBy = request.UserId.ToString(),
                UpdatedBy = request.UserId.ToString()
            };

            await _reconciliationItemRepository.AddAsync(unmatchedBankItem);
        }

        return true;
    }

    private object? ParseBankTransactionFromJson(string? bankReferenceData)
    {
        if (string.IsNullOrWhiteSpace(bankReferenceData))
            return null;

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<object>(bankReferenceData);
        }
        catch
        {
            return null;
        }
    }
}