using System.Data;
using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categorization.Services;
using MyMascada.Application.Features.Transactions.DTOs;
using MyMascada.Application.Features.Transactions.Mappings;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Domain.Common;

namespace MyMascada.Application.Features.Transactions.Commands;

/// <summary>
/// Updates a transaction's editable fields.
/// Note on splits: if the transaction has splits and the amount actually changes,
/// all splits are cleared (soft-deleted) because they would no longer sum to the
/// new amount — an invariant enforced by UpdateTransactionSplitsCommand. The web
/// frontend has no splits UI, so rejecting amount edits on split transactions
/// would strand web users; clients that care about splits must re-apply them via
/// PUT /transactions/{id}/splits after changing the amount.
/// </summary>
public class UpdateTransactionCommand : IRequest<TransactionDto>, ITransactionBaseCommand
{
    public Guid UserId { get; set; }
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? UserDescription { get; set; }
    public TransactionStatus Status { get; set; }
    public string? Notes { get; set; }
    public string? Location { get; set; }
    public string? Tags { get; set; }
    public int? CategoryId { get; set; }
}

public class UpdateTransactionCommandHandler : IRequestHandler<UpdateTransactionCommand, TransactionDto>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ITransferRepository _transferRepository;
    private readonly IAccountAccessService _accountAccessService;
    private readonly ICategorizationHistoryService _historyService;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateTransactionCommandHandler(
        ITransactionRepository transactionRepository,
        ICategoryRepository categoryRepository,
        ITransferRepository transferRepository,
        IAccountAccessService accountAccessService,
        ICategorizationHistoryService historyService,
        IUnitOfWork unitOfWork)
    {
        _transactionRepository = transactionRepository;
        _categoryRepository = categoryRepository;
        _transferRepository = transferRepository;
        _accountAccessService = accountAccessService;
        _historyService = historyService;
        _unitOfWork = unitOfWork;
    }

    public async Task<TransactionDto> Handle(UpdateTransactionCommand request, CancellationToken cancellationToken)
    {
        // Get existing transaction WITHOUT splits. Loading splits here would leave
        // them tracked on the context for every edit, and UpdateAsync marks the whole
        // tracked graph Modified — a non-amount edit could then write stale split rows
        // back (IsDeleted = false) over a concurrent split replacement. Splits are
        // loaded only inside the serializable amount-change branch below.
        var transaction = await _transactionRepository.GetByIdAsync(request.Id, request.UserId);
        if (transaction == null)
        {
            throw new ArgumentException($"Transaction with ID {request.Id} not found or does not belong to user");
        }

        // Verify the user has modify permission on the transaction's account (owner or Manager role)
        if (!await _accountAccessService.CanModifyAccountAsync(request.UserId, transaction.AccountId))
        {
            throw new UnauthorizedAccessException("You do not have permission to update transactions on this account.");
        }

        // Validate category if provided
        if (request.CategoryId.HasValue)
        {
            var categoryExists = await _categoryRepository.ExistsAsync(request.CategoryId.Value, request.UserId);
            if (!categoryExists)
            {
                throw new ArgumentException($"Category with ID {request.CategoryId} not found or does not belong to user");
            }
        }

        // Check if this is a transfer transaction
        bool isTransfer = transaction.IsTransfer();
        var relatedTransaction = default(Domain.Entities.Transaction);
        var transfer = default(Domain.Entities.Transfer);

        if (isTransfer)
        {
            // Get the related transaction and transfer
            if (transaction.RelatedTransactionId.HasValue)
            {
                relatedTransaction = await _transactionRepository.GetByIdAsync(transaction.RelatedTransactionId.Value, request.UserId);
            }
            
            if (transaction.TransferId.HasValue)
            {
                transfer = await _transferRepository.GetByTransferIdAsync(transaction.TransferId.Value, request.UserId);
            }

            // Validate we can update this transfer
            if (relatedTransaction == null || transfer == null)
            {
                throw new ArgumentException("Cannot update transfer transaction - related transaction or transfer not found");
            }

            // For transfers, certain properties should be synchronized
            // Don't allow category changes for transfer components
            if (request.CategoryId.HasValue)
            {
                throw new ArgumentException("Transfer transactions cannot be assigned to categories");
            }
        }

        // Store originals before mutation
        var originalAmount = transaction.Amount;
        var amountChanged = Math.Abs(originalAmount - request.Amount) > 0.01m;
        var originalCategoryId = transaction.CategoryId;
        var originalDescription = transaction.Description;

        // Amount edits must serialize against concurrent PUT /splits replacements:
        // otherwise this handler clears only the splits it loaded, while a replace
        // (validated against the old amount) can insert a fresh active set that
        // commits after this save, leaving active splits that sum to the OLD amount.
        // Serializable isolation (same mechanism as UpdateTransactionSplitsCommand)
        // makes the database abort one of the two conflicting requests instead.
        // Deliberately scoped to amount changes only; other field edits don't touch
        // splits and don't need it. Disposing without Commit rolls back.
        await using IUnitOfWorkTransaction? dbTransaction = originalAmount != request.Amount
            ? await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        if (dbTransaction != null)
        {
            // The ONLY place this handler loads splits: inside the open serializable
            // transaction, so the database registers the read (the initial load above
            // deliberately excludes splits — see comment there). The identity map
            // attaches the loaded splits to the already-tracked transaction instance.
            await _transactionRepository.GetByIdWithSplitsAsync(request.Id, request.UserId);
        }

        // Update transaction properties
        transaction.Amount = request.Amount;
        transaction.TransactionDate = DateTimeProvider.ToUtc(request.TransactionDate);
        transaction.Description = request.Description;
        transaction.UserDescription = request.UserDescription;
        transaction.Status = request.Status;
        transaction.Notes = request.Notes;
        transaction.Location = request.Location;
        transaction.Tags = request.Tags;
        
        // Only set category if not a transfer
        if (!isTransfer)
        {
            transaction.CategoryId = request.CategoryId;
        }

        // Splits must sum to the transaction amount exactly (invariant enforced by
        // UpdateTransactionSplitsCommand). If the amount actually changed, clear the
        // splits (soft-delete, same pattern as the replace logic) rather than reject,
        // since the web frontend has no splits UI to fix them. Exact comparison on
        // purpose: even a sub-cent change breaks the exact-sum invariant.
        if (originalAmount != request.Amount)
        {
            var now = DateTimeProvider.UtcNow;
            foreach (var split in transaction.Splits.Where(s => !s.IsDeleted))
            {
                split.IsDeleted = true;
                split.DeletedAt = now;
                split.UpdatedAt = now;
            }
        }

        transaction.UpdatedAt = DateTimeProvider.UtcNow;

        await _transactionRepository.UpdateAsync(transaction);

        // If this is a transfer and amount changed, update the related transaction and transfer
        if (isTransfer && amountChanged && relatedTransaction != null && transfer != null)
        {
            var newAbsoluteAmount = Math.Abs(request.Amount);
            
            // Update related transaction amount (opposite sign)
            relatedTransaction.Amount = transaction.IsTransferSource ? newAbsoluteAmount : -newAbsoluteAmount;
            relatedTransaction.UpdatedAt = DateTimeProvider.UtcNow;
            
            // Update transfer amount
            transfer.Amount = newAbsoluteAmount;
            transfer.UpdatedAt = DateTime.UtcNow;
            
            // Save both related transaction and transfer
            await _transactionRepository.UpdateAsync(relatedTransaction);
            await _transferRepository.UpdateAsync(transfer);
        }

        // If this is a transfer, also sync certain properties to the related transaction
        if (isTransfer && relatedTransaction != null)
        {
            // Sync date, notes, and other metadata (but not description as they might be different)
            relatedTransaction.TransactionDate = DateTimeProvider.ToUtc(request.TransactionDate);
            relatedTransaction.Status = request.Status;
            relatedTransaction.UpdatedAt = DateTimeProvider.UtcNow;
            
            await _transactionRepository.UpdateAsync(relatedTransaction);
        }

        await _transactionRepository.SaveChangesAsync();

        if (dbTransaction != null)
        {
            await dbTransaction.CommitAsync(cancellationToken);
        }

        // Record categorization history (best-effort — transaction update already persisted above)
        var categoryChanged = originalCategoryId != request.CategoryId;
        var descriptionChanged = originalDescription != request.Description;
        if (!isTransfer && request.CategoryId.HasValue && (categoryChanged || descriptionChanged))
        {
            try
            {
                await _historyService.RecordCategorizationAsync(
                    request.UserId,
                    transaction.Description,
                    request.CategoryId.Value,
                    CategorizationHistorySource.Manual,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // History service already logs internally; swallow to avoid failing
                // the user's transaction update which was already saved.
            }
        }

        // Return updated DTO
        return TransactionMapper.ToDto(transaction);
    }
}