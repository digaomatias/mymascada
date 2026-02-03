using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Transfers.DTOs;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Transfers.Commands;

/// <summary>
/// Handler for creating transfers between accounts
/// </summary>
public class CreateTransferCommandHandler : IRequestHandler<CreateTransferCommand, TransferDto>
{
    private readonly ITransferRepository _transferRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountRepository _accountRepository;

    public CreateTransferCommandHandler(
        ITransferRepository transferRepository,
        ITransactionRepository transactionRepository,
        IAccountRepository accountRepository)
    {
        _transferRepository = transferRepository;
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
    }

    public async Task<TransferDto> Handle(CreateTransferCommand request, CancellationToken cancellationToken)
    {
        // Validate accounts exist and belong to user
        var sourceAccount = await _accountRepository.GetByIdAsync(request.SourceAccountId, request.UserId);
        if (sourceAccount == null)
            throw new ArgumentException($"Source account {request.SourceAccountId} not found");

        var destinationAccount = await _accountRepository.GetByIdAsync(request.DestinationAccountId, request.UserId);
        if (destinationAccount == null)
            throw new ArgumentException($"Destination account {request.DestinationAccountId} not found");

        // Validate not transferring to same account
        if (request.SourceAccountId == request.DestinationAccountId)
            throw new ArgumentException("Cannot transfer to the same account");

        // Create Transfer entity
        var transfer = new Transfer
        {
            TransferId = Guid.NewGuid(),
            Amount = request.Amount,
            Currency = request.Currency,
            ExchangeRate = request.ExchangeRate,
            FeeAmount = request.FeeAmount,
            Description = request.Description,
            Notes = request.Notes,
            Status = TransferStatus.Pending,
            TransferDate = request.TransferDate,
            SourceAccountId = request.SourceAccountId,
            DestinationAccountId = request.DestinationAccountId,
            UserId = request.UserId
        };

        // Save transfer
        var savedTransfer = await _transferRepository.AddAsync(transfer);

        // Create source transaction (expense - negative amount)
        var sourceTransaction = new Transaction
        {
            Amount = -request.Amount, // Negative for expense
            TransactionDate = request.TransferDate,
            Description = request.Description ?? $"Transfer to {destinationAccount.Name}",
            Status = TransactionStatus.Cleared,
            Source = TransactionSource.Manual,
            Notes = request.Notes,
            AccountId = request.SourceAccountId,
            Type = TransactionType.TransferComponent,
            TransferId = transfer.TransferId,
            IsTransferSource = true
        };

        // Create destination transaction (income - positive amount)
        var destinationAmount = transfer.GetDestinationAmount();
        var destinationTransaction = new Transaction
        {
            Amount = destinationAmount, // Positive for income
            TransactionDate = request.TransferDate,
            Description = request.Description ?? $"Transfer from {sourceAccount.Name}",
            Status = TransactionStatus.Cleared,
            Source = TransactionSource.Manual,
            Notes = request.Notes,
            AccountId = request.DestinationAccountId,
            Type = TransactionType.TransferComponent,
            TransferId = transfer.TransferId,
            IsTransferSource = false
        };

        // Save transactions
        await _transactionRepository.AddAsync(sourceTransaction);
        await _transactionRepository.AddAsync(destinationTransaction);

        // Mark transfer as completed
        transfer.MarkAsCompleted();
        await _transferRepository.UpdateAsync(transfer);

        // Return DTO
        return new TransferDto
        {
            Id = savedTransfer.Id,
            TransferId = savedTransfer.TransferId,
            Amount = savedTransfer.Amount,
            Currency = savedTransfer.Currency,
            ExchangeRate = savedTransfer.ExchangeRate,
            FeeAmount = savedTransfer.FeeAmount,
            Description = savedTransfer.Description,
            Notes = savedTransfer.Notes,
            Status = savedTransfer.Status,
            TransferDate = savedTransfer.TransferDate,
            CompletedDate = savedTransfer.CompletedDate,
            SourceAccount = new TransferAccountDto
            {
                Id = sourceAccount.Id,
                Name = sourceAccount.Name,
                Currency = sourceAccount.Currency,
                Type = sourceAccount.Type.ToString()
            },
            DestinationAccount = new TransferAccountDto
            {
                Id = destinationAccount.Id,
                Name = destinationAccount.Name,
                Currency = destinationAccount.Currency,
                Type = destinationAccount.Type.ToString()
            },
            Transactions = new List<TransferTransactionDto>
            {
                new()
                {
                    Id = sourceTransaction.Id,
                    Amount = sourceTransaction.Amount,
                    Description = sourceTransaction.Description,
                    IsTransferSource = true,
                    AccountId = sourceTransaction.AccountId,
                    Type = sourceTransaction.Type
                },
                new()
                {
                    Id = destinationTransaction.Id,
                    Amount = destinationTransaction.Amount,
                    Description = destinationTransaction.Description,
                    IsTransferSource = false,
                    AccountId = destinationTransaction.AccountId,
                    Type = destinationTransaction.Type
                }
            },
            IsMultiCurrency = savedTransfer.IsMultiCurrency(),
            DestinationAmount = destinationAmount,
            CreatedAt = savedTransfer.CreatedAt,
            UpdatedAt = savedTransfer.UpdatedAt
        };
    }
}