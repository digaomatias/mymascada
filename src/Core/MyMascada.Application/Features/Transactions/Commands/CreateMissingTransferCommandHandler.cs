using MediatR;
using AutoMapper;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Transactions.DTOs;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Transactions.Commands;

/// <summary>
/// Handler for creating missing transfer transactions
/// </summary>
public class CreateMissingTransferCommandHandler : IRequestHandler<CreateMissingTransferCommand, ConfirmTransfersResponse>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ITransferRepository _transferRepository;
    private readonly IMapper _mapper;

    public CreateMissingTransferCommandHandler(
        ITransactionRepository transactionRepository,
        IAccountRepository accountRepository,
        ITransferRepository transferRepository,
        IMapper mapper)
    {
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
        _transferRepository = transferRepository;
        _mapper = mapper;
    }

    public async Task<ConfirmTransfersResponse> Handle(CreateMissingTransferCommand request, CancellationToken cancellationToken)
    {
        var response = new ConfirmTransfersResponse { Success = true };

        try
        {
            // Get the existing transaction
            var existingTransaction = await _transactionRepository.GetByIdAsync(request.ExistingTransactionId, request.UserId);
            if (existingTransaction == null)
            {
                response.Success = false;
                response.Message = "Existing transaction not found";
                response.Errors.Add("Transaction not found");
                return response;
            }

            // Validate the missing account exists and belongs to user
            var missingAccount = await _accountRepository.GetByIdAsync(request.MissingAccountId, request.UserId);
            if (missingAccount == null)
            {
                response.Success = false;
                response.Message = "Destination account not found";
                response.Errors.Add("Account not found");
                return response;
            }

            // Validate not creating transfer to same account
            if (existingTransaction.AccountId == request.MissingAccountId)
            {
                response.Success = false;
                response.Message = "Cannot create transfer to the same account";
                response.Errors.Add("Source and destination accounts must be different");
                return response;
            }

            // Determine transfer direction and amounts
            var isExistingSource = existingTransaction.Amount < 0;
            var transferAmount = Math.Abs(existingTransaction.Amount);
            
            // Get source and destination accounts
            var sourceAccount = isExistingSource ? existingTransaction.Account : missingAccount;
            var destinationAccount = isExistingSource ? missingAccount : existingTransaction.Account;
            var sourceAccountId = isExistingSource ? existingTransaction.AccountId : request.MissingAccountId;
            var destinationAccountId = isExistingSource ? request.MissingAccountId : existingTransaction.AccountId;

            // Create Transfer entity
            var transferDate = request.TransactionDate ?? existingTransaction.TransactionDate;
            var transfer = new Transfer
            {
                TransferId = Guid.NewGuid(),
                Amount = transferAmount,
                Currency = sourceAccount.Currency,
                Description = request.Description ?? $"Transfer {(isExistingSource ? "to" : "from")} {(isExistingSource ? destinationAccount.Name : sourceAccount.Name)}",
                Notes = request.Notes,
                Status = TransferStatus.Completed,
                TransferDate = DateTime.SpecifyKind(transferDate, DateTimeKind.Utc),
                CompletedDate = DateTime.UtcNow,
                SourceAccountId = sourceAccountId,
                DestinationAccountId = destinationAccountId,
                UserId = request.UserId
            };

            // Save transfer
            await _transferRepository.AddAsync(transfer);

            // Update existing transaction to be part of the transfer
            existingTransaction.TransferId = transfer.TransferId;
            existingTransaction.Type = TransactionType.TransferComponent;
            existingTransaction.IsTransferSource = isExistingSource;
            existingTransaction.UpdatedAt = DateTime.UtcNow;
            await _transactionRepository.UpdateAsync(existingTransaction);

            // Create the missing transaction
            var missingTransactionAmount = isExistingSource ? transferAmount : -transferAmount;
            var missingTransaction = new Transaction
            {
                Amount = missingTransactionAmount,
                TransactionDate = DateTime.SpecifyKind(transferDate, DateTimeKind.Utc),
                Description = request.Description ?? $"Transfer {(isExistingSource ? "from" : "to")} {(isExistingSource ? sourceAccount.Name : destinationAccount.Name)}",
                UserDescription = existingTransaction.UserDescription,
                Status = TransactionStatus.Cleared,
                Source = TransactionSource.Manual,
                Notes = request.Notes ?? existingTransaction.Notes,
                Location = existingTransaction.Location,
                Tags = existingTransaction.Tags,
                AccountId = request.MissingAccountId,
                Type = TransactionType.TransferComponent,
                TransferId = transfer.TransferId,
                IsTransferSource = !isExistingSource,
                RelatedTransactionId = existingTransaction.Id,
                IsReviewed = false,
                IsExcluded = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Save the missing transaction
            await _transactionRepository.AddAsync(missingTransaction);

            // Update existing transaction to reference the new one
            existingTransaction.RelatedTransactionId = missingTransaction.Id;
            await _transactionRepository.UpdateAsync(existingTransaction);

            // Save all changes
            await _transactionRepository.SaveChangesAsync();

            response.TransfersCreated = 1;
            response.TransactionsUpdated = 2; // existing + new
            response.Message = $"Successfully created transfer from {sourceAccount.Name} to {destinationAccount.Name}";

            return response;
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = "Failed to create missing transfer transaction";
            response.Errors.Add($"Error: {ex.Message}");
            return response;
        }
    }
}