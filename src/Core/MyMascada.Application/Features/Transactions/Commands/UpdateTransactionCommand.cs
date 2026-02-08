using AutoMapper;
using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Transactions.DTOs;
using MyMascada.Domain.Enums;
using MyMascada.Domain.Common;

namespace MyMascada.Application.Features.Transactions.Commands;

public class UpdateTransactionCommand : IRequest<TransactionDto>
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
    private readonly IMapper _mapper;

    public UpdateTransactionCommandHandler(
        ITransactionRepository transactionRepository,
        ICategoryRepository categoryRepository,
        ITransferRepository transferRepository,
        IAccountAccessService accountAccessService,
        IMapper mapper)
    {
        _transactionRepository = transactionRepository;
        _mapper = mapper;
        _categoryRepository = categoryRepository;
        _transferRepository = transferRepository;
        _accountAccessService = accountAccessService;
    }

    public async Task<TransactionDto> Handle(UpdateTransactionCommand request, CancellationToken cancellationToken)
    {
        // Get existing transaction
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

        // Store original amount for transfer amount calculations
        var originalAmount = transaction.Amount;
        var amountChanged = Math.Abs(originalAmount - request.Amount) > 0.01m;

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

        // Return updated DTO
        return _mapper.Map<TransactionDto>(transaction);
    }
}