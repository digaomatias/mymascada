using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Transactions.DTOs;
using MyMascada.Application.Features.Transactions.Mappings;
using MyMascada.Domain.Common;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Transactions.Commands;

/// <summary>
/// Atomically replaces the splits of a transaction. A null or empty
/// <see cref="Splits"/> list clears all splits (un-splits the transaction).
/// Splitting does NOT change the parent transaction's CategoryId; analytics
/// and budgets currently source category from the parent transaction only.
/// </summary>
public class UpdateTransactionSplitsCommand : IRequest<TransactionDto>
{
    public Guid UserId { get; set; }
    public int TransactionId { get; set; }
    public List<TransactionSplitInputDto>? Splits { get; set; }
}

public class UpdateTransactionSplitsCommandHandler : IRequestHandler<UpdateTransactionSplitsCommand, TransactionDto>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IAccountAccessService _accountAccessService;

    public UpdateTransactionSplitsCommandHandler(
        ITransactionRepository transactionRepository,
        ICategoryRepository categoryRepository,
        IAccountAccessService accountAccessService)
    {
        _transactionRepository = transactionRepository;
        _categoryRepository = categoryRepository;
        _accountAccessService = accountAccessService;
    }

    public async Task<TransactionDto> Handle(UpdateTransactionSplitsCommand request, CancellationToken cancellationToken)
    {
        var transaction = await _transactionRepository.GetByIdWithSplitsAsync(request.TransactionId, request.UserId);
        if (transaction == null)
        {
            throw new ArgumentException($"Transaction with ID {request.TransactionId} not found or does not belong to user");
        }

        // Verify the user has modify permission on the transaction's account (owner or Manager role)
        if (!await _accountAccessService.CanModifyAccountAsync(request.UserId, transaction.AccountId))
        {
            throw new UnauthorizedAccessException("You do not have permission to update transactions on this account.");
        }

        // Transfer components cannot carry categories (see UpdateTransactionCommand),
        // so they cannot be split either.
        if (transaction.IsTransfer())
        {
            throw new ArgumentException("Transfer transactions cannot be split");
        }

        var newSplits = request.Splits ?? new List<TransactionSplitInputDto>();

        if (newSplits.Count > 0)
        {
            ValidateSplits(transaction, newSplits);
            await ValidateCategoriesAsync(request.UserId, newSplits);
        }

        var now = DateTimeProvider.UtcNow;

        // Soft-delete all currently active splits (replace semantics).
        foreach (var existing in transaction.Splits.Where(s => !s.IsDeleted))
        {
            existing.IsDeleted = true;
            existing.DeletedAt = now;
            existing.UpdatedAt = now;
        }

        foreach (var item in newSplits)
        {
            transaction.Splits.Add(new TransactionSplit
            {
                TransactionId = transaction.Id,
                CategoryId = item.CategoryId,
                Amount = item.Amount,
                Description = item.Description,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        transaction.UpdatedAt = now;

        // Single SaveChanges inside UpdateAsync -> the replace is atomic.
        await _transactionRepository.UpdateAsync(transaction);

        // Re-load so newly added splits have their Category navigation populated
        // for the response DTO (CategoryName/CategoryColor).
        var updated = await _transactionRepository.GetByIdWithSplitsAsync(request.TransactionId, request.UserId);
        return TransactionMapper.ToDto(updated ?? transaction);
    }

    private static void ValidateSplits(Transaction transaction, List<TransactionSplitInputDto> splits)
    {
        if (transaction.Amount == 0)
        {
            throw new ArgumentException("A zero-amount transaction cannot be split");
        }

        var transactionSign = Math.Sign(transaction.Amount);
        if (splits.Any(s => Math.Sign(s.Amount) != transactionSign))
        {
            throw new ArgumentException("Each split amount must be non-zero and have the same sign as the transaction amount");
        }

        var sum = splits.Sum(s => s.Amount);
        if (sum != transaction.Amount)
        {
            throw new ArgumentException($"Split amounts must sum to the transaction amount exactly. Expected {transaction.Amount} but got {sum}");
        }
    }

    private async Task ValidateCategoriesAsync(Guid userId, List<TransactionSplitInputDto> splits)
    {
        foreach (var categoryId in splits.Select(s => s.CategoryId).Distinct())
        {
            var categoryExists = await _categoryRepository.ExistsAsync(categoryId, userId);
            if (!categoryExists)
            {
                throw new ArgumentException($"Category with ID {categoryId} not found or does not belong to user");
            }
        }
    }
}
