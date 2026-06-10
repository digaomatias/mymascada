using System.Data;
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
/// Note: editing the transaction's amount via UpdateTransactionCommand clears
/// existing splits (they would no longer sum to the new amount), so clients
/// must re-apply splits after an amount change.
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
    private readonly IUnitOfWork _unitOfWork;

    public UpdateTransactionSplitsCommandHandler(
        ITransactionRepository transactionRepository,
        ICategoryRepository categoryRepository,
        IAccountAccessService accountAccessService,
        IUnitOfWork unitOfWork)
    {
        _transactionRepository = transactionRepository;
        _categoryRepository = categoryRepository;
        _accountAccessService = accountAccessService;
        _unitOfWork = unitOfWork;
    }

    public async Task<TransactionDto> Handle(UpdateTransactionSplitsCommand request, CancellationToken cancellationToken)
    {
        // Serializable so two overlapping replace requests for the same transaction
        // cannot both read the same active split set and each append their own rows
        // (which would leave two active sets that no longer sum to the amount).
        // The database aborts one of the conflicting transactions with a
        // serialization failure instead. Disposing without Commit rolls back.
        await using var dbTransaction = await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

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

        await _transactionRepository.UpdateAsync(transaction);

        // Re-load so newly added splits have their Category navigation populated
        // for the response DTO (CategoryName/CategoryColor). Inside the open
        // transaction this reads our own writes.
        var updated = await _transactionRepository.GetByIdWithSplitsAsync(request.TransactionId, request.UserId);
        if (updated == null)
        {
            // Should be impossible inside the open transaction; fail loudly rather
            // than return a DTO with unpopulated split category names/colors.
            throw new InvalidOperationException($"Transaction {request.TransactionId} could not be reloaded after updating splits");
        }

        await dbTransaction.CommitAsync(cancellationToken);

        return TransactionMapper.ToDto(updated);
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
