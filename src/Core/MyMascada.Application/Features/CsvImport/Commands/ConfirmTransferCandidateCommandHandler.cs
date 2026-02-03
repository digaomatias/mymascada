using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.CsvImport.Commands;

/// <summary>
/// Handler for confirming or rejecting transfer candidates
/// </summary>
public class ConfirmTransferCandidateCommandHandler : IRequestHandler<ConfirmTransferCandidateCommand, bool>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ITransferRepository _transferRepository;

    public ConfirmTransferCandidateCommandHandler(
        ITransactionRepository transactionRepository,
        ITransferRepository transferRepository)
    {
        _transactionRepository = transactionRepository;
        _transferRepository = transferRepository;
    }

    public async Task<bool> Handle(ConfirmTransferCandidateCommand request, CancellationToken cancellationToken)
    {
        // Get both transactions
        var debitTransaction = await _transactionRepository.GetByIdAsync(request.DebitTransactionId);
        var creditTransaction = await _transactionRepository.GetByIdAsync(request.CreditTransactionId);

        if (debitTransaction == null || creditTransaction == null)
        {
            return false;
        }

        // Validate that these can be linked as a transfer
        if (!CanCreateTransfer(debitTransaction, creditTransaction))
        {
            return false;
        }

        if (request.IsConfirmed)
        {
            // Create the transfer
            var transfer = new Transfer
            {
                Amount = Math.Abs(debitTransaction.Amount),
                Currency = "USD", // TODO: Get from account or transaction
                Description = request.Description ?? "Transfer detected from CSV import",
                Status = TransferStatus.Completed,
                TransferDate = debitTransaction.TransactionDate < creditTransaction.TransactionDate 
                    ? debitTransaction.TransactionDate 
                    : creditTransaction.TransactionDate,
                CompletedDate = DateTime.UtcNow,
                SourceAccountId = debitTransaction.AccountId,
                DestinationAccountId = creditTransaction.AccountId,
                UserId = request.UserId ?? debitTransaction.Account.UserId
            };

            // Save the transfer
            await _transferRepository.AddAsync(transfer);
            await _transferRepository.SaveChangesAsync();

            // Update both transactions to link to this transfer
            debitTransaction.TransferId = transfer.TransferId;
            debitTransaction.Type = TransactionType.TransferComponent;
            debitTransaction.IsTransferSource = true;
            debitTransaction.UpdatedAt = DateTime.UtcNow;

            creditTransaction.TransferId = transfer.TransferId;
            creditTransaction.Type = TransactionType.TransferComponent;
            creditTransaction.IsTransferSource = false;
            creditTransaction.UpdatedAt = DateTime.UtcNow;

            // Link transactions to each other
            debitTransaction.RelatedTransactionId = creditTransaction.Id;
            creditTransaction.RelatedTransactionId = debitTransaction.Id;

            await _transactionRepository.UpdateAsync(debitTransaction);
            await _transactionRepository.UpdateAsync(creditTransaction);
            await _transactionRepository.SaveChangesAsync();

            return true;
        }
        else
        {
            // User rejected the suggestion - could log this for learning
            // For now, just return true to indicate the request was processed
            return true;
        }
    }

    /// <summary>
    /// Validates that two transactions can be linked as a transfer
    /// </summary>
    private bool CanCreateTransfer(Transaction debitTransaction, Transaction creditTransaction)
    {
        // Must be from different accounts
        if (debitTransaction.AccountId == creditTransaction.AccountId)
            return false;

        // One must be negative (debit), one positive (credit)
        if (debitTransaction.Amount >= 0 || creditTransaction.Amount <= 0)
            return false;

        // Neither should already be part of a transfer
        if (debitTransaction.TransferId.HasValue || creditTransaction.TransferId.HasValue)
            return false;

        // Amounts should be similar (allowing for small fees)
        var debitAmount = Math.Abs(debitTransaction.Amount);
        var creditAmount = Math.Abs(creditTransaction.Amount);
        var difference = Math.Abs(debitAmount - creditAmount);
        var percentDifference = difference / Math.Max(debitAmount, creditAmount);

        return percentDifference <= 0.05m; // 5% tolerance
    }
}