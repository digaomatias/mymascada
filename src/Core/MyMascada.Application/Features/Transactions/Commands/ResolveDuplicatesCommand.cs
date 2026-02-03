using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Transactions.DTOs;

namespace MyMascada.Application.Features.Transactions.Commands;

public class ResolveDuplicatesCommand : IRequest<ResolveDuplicatesResponse>
{
    public Guid UserId { get; set; }
    public List<DuplicateResolutionItem> Resolutions { get; set; } = new();
}

public class DuplicateResolutionItem
{
    public string GroupId { get; set; } = string.Empty;
    public List<int> TransactionIdsToKeep { get; set; } = new();
    public List<int> TransactionIdsToDelete { get; set; } = new();
    public bool MarkAsNotDuplicate { get; set; } = false;
    public string? Notes { get; set; }
}

public class ResolveDuplicatesCommandHandler : IRequestHandler<ResolveDuplicatesCommand, ResolveDuplicatesResponse>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IDuplicateExclusionRepository _duplicateExclusionRepository;

    public ResolveDuplicatesCommandHandler(
        ITransactionRepository transactionRepository,
        IDuplicateExclusionRepository duplicateExclusionRepository)
    {
        _transactionRepository = transactionRepository;
        _duplicateExclusionRepository = duplicateExclusionRepository;
    }

    public async Task<ResolveDuplicatesResponse> Handle(ResolveDuplicatesCommand request, CancellationToken cancellationToken)
    {
        var totalDeleted = 0;
        var totalKept = 0;
        var errors = new List<string>();

        foreach (var resolution in request.Resolutions)
        {
            try
            {
                if (resolution.MarkAsNotDuplicate)
                {
                    // Create a duplicate exclusion to prevent this group from appearing again
                    var exclusionTransactionIds = resolution.TransactionIdsToKeep
                        .Concat(resolution.TransactionIdsToDelete)
                        .Distinct()
                        .ToList();

                    var exclusion = new Domain.Entities.DuplicateExclusion
                    {
                        UserId = request.UserId,
                        Notes = resolution.Notes ?? "Marked as not duplicate",
                        OriginalConfidence = 1.0m, // We don't have confidence info here, use max
                        ExcludedAt = DateTime.UtcNow
                    };
                    
                    exclusion.SetTransactionIdsList(exclusionTransactionIds);
                    
                    await _duplicateExclusionRepository.AddAsync(exclusion);
                    continue;
                }

                // Validate that transactions exist and belong to the user
                var allTransactionIds = resolution.TransactionIdsToKeep
                    .Concat(resolution.TransactionIdsToDelete)
                    .Distinct()
                    .ToList();

                var transactions = await _transactionRepository.GetTransactionsByIdsAsync(
                    allTransactionIds, request.UserId, cancellationToken);

                var foundIds = transactions.Select(t => t.Id).ToHashSet();
                var missingIds = allTransactionIds.Where(id => !foundIds.Contains(id)).ToList();

                if (missingIds.Any())
                {
                    errors.Add($"Group {resolution.GroupId}: Transactions not found or access denied: {string.Join(", ", missingIds)}");
                    continue;
                }

                // Delete the specified transactions
                foreach (var transactionId in resolution.TransactionIdsToDelete)
                {
                    var transaction = transactions.FirstOrDefault(t => t.Id == transactionId);
                    if (transaction == null)
                    {
                        errors.Add($"Group {resolution.GroupId}: Cannot delete transaction {transactionId} - transaction not found");
                        continue;
                    }
                    
                    if (transaction.IsDeleted)
                    {
                        // Transaction already deleted, skip but don't error
                        continue;
                    }
                    
                    transaction.IsDeleted = true;
                    transaction.UpdatedAt = DateTime.UtcNow;
                    transaction.UpdatedBy = request.UserId.ToString();
                    
                    await _transactionRepository.UpdateAsync(transaction);
                    totalDeleted++;
                }

                // Update kept transactions with notes if provided
                if (!string.IsNullOrEmpty(resolution.Notes))
                {
                    foreach (var transactionId in resolution.TransactionIdsToKeep)
                    {
                        var transaction = transactions.FirstOrDefault(t => t.Id == transactionId && !t.IsDeleted);
                        if (transaction == null)
                        {
                            errors.Add($"Group {resolution.GroupId}: Cannot update notes for transaction {transactionId} - transaction not found or already deleted");
                            continue;
                        }
                        
                        // Append resolution notes to existing notes
                        var existingNotes = transaction.Notes ?? "";
                        var newNotes = string.IsNullOrEmpty(existingNotes) 
                            ? $"Duplicate resolution: {resolution.Notes}"
                            : $"{existingNotes}\nDuplicate resolution: {resolution.Notes}";
                        
                        transaction.Notes = newNotes;
                        transaction.UpdatedAt = DateTime.UtcNow;
                        transaction.UpdatedBy = request.UserId.ToString();
                        
                        await _transactionRepository.UpdateAsync(transaction);
                    }
                }

                totalKept += resolution.TransactionIdsToKeep.Count;
            }
            catch (Exception ex)
            {
                errors.Add($"Group {resolution.GroupId}: {ex.Message}");
            }
        }

        await _transactionRepository.SaveChangesAsync();

        return new ResolveDuplicatesResponse
        {
            Success = !errors.Any(),
            Message = errors.Any() 
                ? $"Completed with {errors.Count} error(s): {string.Join("; ", errors)}"
                : "All duplicates resolved successfully",
            TransactionsDeleted = totalDeleted,
            TransactionsKept = totalKept,
            Errors = errors
        };
    }
}