using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Transactions.DTOs;

namespace MyMascada.Application.Features.Transactions.Commands;

public class BulkDeleteTransactionsCommand : IRequest<BulkDeleteTransactionsResponse>
{
    public Guid UserId { get; set; }
    public List<int> TransactionIds { get; set; } = new();
    public string? Reason { get; set; }
}

public class BulkDeleteTransactionsCommandHandler : IRequestHandler<BulkDeleteTransactionsCommand, BulkDeleteTransactionsResponse>
{
    private readonly ITransactionRepository _transactionRepository;

    public BulkDeleteTransactionsCommandHandler(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    public async Task<BulkDeleteTransactionsResponse> Handle(BulkDeleteTransactionsCommand request, CancellationToken cancellationToken)
    {
        var totalDeleted = 0;
        var errors = new List<string>();

        if (!request.TransactionIds.Any())
        {
            return new BulkDeleteTransactionsResponse
            {
                Success = false,
                Message = "No transaction IDs provided",
                TransactionsDeleted = 0,
                Errors = new List<string> { "No transaction IDs provided" }
            };
        }

        try
        {
            // Validate that transactions exist and belong to the user
            var transactions = await _transactionRepository.GetTransactionsByIdsAsync(
                request.TransactionIds, request.UserId, cancellationToken);

            var foundIds = transactions.Select(t => t.Id).ToHashSet();
            var missingIds = request.TransactionIds.Where(id => !foundIds.Contains(id)).ToList();

            if (missingIds.Any())
            {
                errors.Add($"Transactions not found or access denied: {string.Join(", ", missingIds)}");
            }

            // Delete the found transactions
            foreach (var transaction in transactions)
            {
                // Use soft delete
                transaction.IsDeleted = true;
                transaction.UpdatedAt = DateTime.UtcNow;
                transaction.UpdatedBy = request.UserId.ToString();
                
                // Add reason to notes if provided
                if (!string.IsNullOrEmpty(request.Reason))
                {
                    var existingNotes = transaction.Notes ?? "";
                    var newNotes = string.IsNullOrEmpty(existingNotes) 
                        ? $"Bulk delete: {request.Reason}"
                        : $"{existingNotes}\nBulk delete: {request.Reason}";
                    
                    transaction.Notes = newNotes;
                }
                
                await _transactionRepository.UpdateAsync(transaction);
                totalDeleted++;
            }

            await _transactionRepository.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to delete transactions: {ex.Message}");
        }

        return new BulkDeleteTransactionsResponse
        {
            Success = !errors.Any(),
            Message = errors.Any() 
                ? $"Completed with {errors.Count} error(s): {string.Join("; ", errors)}"
                : $"Successfully deleted {totalDeleted} transaction(s)",
            TransactionsDeleted = totalDeleted,
            Errors = errors
        };
    }
}