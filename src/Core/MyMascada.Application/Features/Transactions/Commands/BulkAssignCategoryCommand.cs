using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Transactions.DTOs;

namespace MyMascada.Application.Features.Transactions.Commands;

public class BulkAssignCategoryCommand : IRequest<BulkAssignCategoryResponse>
{
    public Guid UserId { get; set; }
    public List<int> TransactionIds { get; set; } = new();
    public int CategoryId { get; set; }
}

public class BulkAssignCategoryCommandHandler : IRequestHandler<BulkAssignCategoryCommand, BulkAssignCategoryResponse>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IAccountAccessService _accountAccessService;

    public BulkAssignCategoryCommandHandler(
        ITransactionRepository transactionRepository,
        ICategoryRepository categoryRepository,
        IAccountAccessService accountAccessService)
    {
        _transactionRepository = transactionRepository;
        _categoryRepository = categoryRepository;
        _accountAccessService = accountAccessService;
    }

    public async Task<BulkAssignCategoryResponse> Handle(BulkAssignCategoryCommand request, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (request.TransactionIds == null || request.TransactionIds.Count == 0)
        {
            return new BulkAssignCategoryResponse
            {
                Success = false,
                Message = "No transaction IDs provided",
                Errors = new List<string> { "No transaction IDs provided" }
            };
        }

        if (!await _categoryRepository.ExistsAsync(request.CategoryId, request.UserId))
        {
            return new BulkAssignCategoryResponse
            {
                Success = false,
                Message = "Category not found or access denied",
                Errors = new List<string> { "Category not found or access denied" }
            };
        }

        var transactions = (await _transactionRepository.GetTransactionsByIdsAsync(
            request.TransactionIds,
            request.UserId,
            cancellationToken)).ToList();

        var foundIds = transactions.Select(t => t.Id).ToHashSet();
        var missingIds = request.TransactionIds.Where(id => !foundIds.Contains(id)).ToList();
        if (missingIds.Count > 0)
        {
            errors.Add($"Transactions not found or access denied: {string.Join(", ", missingIds)}");
        }

        var accountIds = transactions.Select(t => t.AccountId).Distinct().ToList();
        foreach (var accountId in accountIds)
        {
            if (!await _accountAccessService.CanModifyAccountAsync(request.UserId, accountId))
            {
                throw new UnauthorizedAccessException("You do not have permission to update transactions on one or more of these accounts.");
            }
        }

        var updatedCount = 0;
        var now = DateTime.UtcNow;

        foreach (var transaction in transactions)
        {
            if (transaction.TransferId.HasValue)
            {
                errors.Add($"Transaction {transaction.Id} is part of a transfer and cannot be categorized.");
                continue;
            }

            transaction.CategoryId = request.CategoryId;
            transaction.UpdatedAt = now;
            transaction.UpdatedBy = request.UserId.ToString();
            updatedCount++;
        }

        await _transactionRepository.SaveChangesAsync();

        return new BulkAssignCategoryResponse
        {
            Success = errors.Count == 0,
            Message = errors.Count == 0
                ? $"Successfully updated {updatedCount} transaction(s)"
                : $"Updated {updatedCount} transaction(s) with {errors.Count} issue(s)",
            TransactionsUpdated = updatedCount,
            Errors = errors
        };
    }
}
