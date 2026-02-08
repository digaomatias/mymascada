using MediatR;
using AutoMapper;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Transactions.DTOs;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Transactions.Commands;

/// <summary>
/// Handler for linking two existing transactions as a transfer
/// </summary>
public class LinkTransactionsAsTransferCommandHandler : IRequestHandler<LinkTransactionsAsTransferCommand, ConfirmTransfersResponse>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ITransferRepository _transferRepository;
    private readonly IAccountAccessService _accountAccessService;
    private readonly IMapper _mapper;

    public LinkTransactionsAsTransferCommandHandler(
        ITransactionRepository transactionRepository,
        ITransferRepository transferRepository,
        IAccountAccessService accountAccessService,
        IMapper mapper)
    {
        _transactionRepository = transactionRepository;
        _transferRepository = transferRepository;
        _accountAccessService = accountAccessService;
        _mapper = mapper;
    }

    public async Task<ConfirmTransfersResponse> Handle(LinkTransactionsAsTransferCommand request, CancellationToken cancellationToken)
    {
        var response = new ConfirmTransfersResponse { Success = true };

        try
        {
            // Get both transactions
            var sourceTransaction = await _transactionRepository.GetByIdAsync(request.SourceTransactionId, request.UserId);
            var destinationTransaction = await _transactionRepository.GetByIdAsync(request.DestinationTransactionId, request.UserId);

            if (sourceTransaction == null)
            {
                response.Success = false;
                response.Message = "Source transaction not found";
                response.Errors.Add("Source transaction not found");
                return response;
            }

            if (destinationTransaction == null)
            {
                response.Success = false;
                response.Message = "Destination transaction not found";
                response.Errors.Add("Destination transaction not found");
                return response;
            }

            // Verify the user has modify permission on both accounts (owner or Manager role)
            if (!await _accountAccessService.CanModifyAccountAsync(request.UserId, sourceTransaction.AccountId))
            {
                throw new UnauthorizedAccessException("You do not have permission to modify transactions on the source account.");
            }
            if (!await _accountAccessService.CanModifyAccountAsync(request.UserId, destinationTransaction.AccountId))
            {
                throw new UnauthorizedAccessException("You do not have permission to modify transactions on the destination account.");
            }

            // Validate transactions are from different accounts
            if (sourceTransaction.AccountId == destinationTransaction.AccountId)
            {
                response.Success = false;
                response.Message = "Cannot create transfer between transactions from the same account";
                response.Errors.Add("Source and destination transactions must be from different accounts");
                return response;
            }

            // Validate transactions are not already part of transfers
            if (sourceTransaction.TransferId.HasValue || destinationTransaction.TransferId.HasValue)
            {
                response.Success = false;
                response.Message = "One or both transactions are already part of a transfer";
                response.Errors.Add("Transactions are already linked to transfers");
                return response;
            }

            // Determine transfer direction based on amounts
            var isSourceNegative = sourceTransaction.Amount < 0;
            var isDestinationPositive = destinationTransaction.Amount > 0;

            // Validate that amounts make sense for a transfer
            var sourceAmount = Math.Abs(sourceTransaction.Amount);
            var destinationAmount = Math.Abs(destinationTransaction.Amount);
            var amountDifference = Math.Abs(sourceAmount - destinationAmount);
            var tolerance = Math.Max(sourceAmount, destinationAmount) * 0.05m; // 5% tolerance

            if (amountDifference > tolerance)
            {
                response.Success = false;
                response.Message = $"Transaction amounts differ too much (${sourceAmount:F2} vs ${destinationAmount:F2})";
                response.Errors.Add("Transaction amounts should be similar for a valid transfer");
                return response;
            }

            // Use the larger amount as the transfer amount
            var transferAmount = Math.Max(sourceAmount, destinationAmount);

            // Determine which transaction is actually the source (outgoing) and destination (incoming)
            Transaction actualSource, actualDestination;
            if (isSourceNegative && isDestinationPositive)
            {
                // Normal case: source is negative (outgoing), destination is positive (incoming)
                actualSource = sourceTransaction;
                actualDestination = destinationTransaction;
            }
            else if (!isSourceNegative && !isDestinationPositive)
            {
                // Both transactions have same sign - use the one specified as source
                actualSource = sourceTransaction;
                actualDestination = destinationTransaction;
            }
            else
            {
                // Reverse case: destination is negative, source is positive
                actualSource = destinationTransaction;
                actualDestination = sourceTransaction;
            }

            // Create Transfer entity
            var transferDate = actualSource.TransactionDate;
            var transfer = new Transfer
            {
                TransferId = Guid.NewGuid(),
                Amount = transferAmount,
                Currency = actualSource.Account?.Currency ?? "USD",
                Description = request.Description ?? $"Transfer from {actualSource.Account?.Name} to {actualDestination.Account?.Name}",
                Notes = request.Notes,
                Status = TransferStatus.Completed,
                TransferDate = DateTime.SpecifyKind(transferDate, DateTimeKind.Utc),
                CompletedDate = DateTime.UtcNow,
                SourceAccountId = actualSource.AccountId,
                DestinationAccountId = actualDestination.AccountId,
                UserId = request.UserId
            };

            // Save transfer
            await _transferRepository.AddAsync(transfer);

            // Update source transaction
            actualSource.TransferId = transfer.TransferId;
            actualSource.Type = TransactionType.TransferComponent;
            actualSource.IsTransferSource = true;
            actualSource.RelatedTransactionId = actualDestination.Id;
            actualSource.UpdatedAt = DateTime.UtcNow;
            await _transactionRepository.UpdateAsync(actualSource);

            // Update destination transaction
            actualDestination.TransferId = transfer.TransferId;
            actualDestination.Type = TransactionType.TransferComponent;
            actualDestination.IsTransferSource = false;
            actualDestination.RelatedTransactionId = actualSource.Id;
            actualDestination.UpdatedAt = DateTime.UtcNow;
            await _transactionRepository.UpdateAsync(actualDestination);

            // Save all changes
            await _transactionRepository.SaveChangesAsync();

            response.TransfersCreated = 1;
            response.TransactionsUpdated = 2;
            response.Message = $"Successfully linked transactions as transfer from {actualSource.Account?.Name} to {actualDestination.Account?.Name}";

            return response;
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = "Failed to link transactions as transfer";
            response.Errors.Add($"Error: {ex.Message}");
            return response;
        }
    }
}