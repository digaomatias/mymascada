using MediatR;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.Transfers.Commands;

/// <summary>
/// Handler for reversing transfer direction
/// </summary>
public class ReverseTransferCommandHandler : IRequestHandler<ReverseTransferCommand, ReverseTransferResponse>
{
    private readonly ITransferRepository _transferRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountAccessService _accountAccessService;

    public ReverseTransferCommandHandler(
        ITransferRepository transferRepository,
        ITransactionRepository transactionRepository,
        IAccountAccessService accountAccessService)
    {
        _transferRepository = transferRepository;
        _transactionRepository = transactionRepository;
        _accountAccessService = accountAccessService;
    }

    public async Task<ReverseTransferResponse> Handle(ReverseTransferCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Get the transfer
            var transfer = await _transferRepository.GetByIdAsync(request.TransferId, request.UserId);
            if (transfer == null)
            {
                return new ReverseTransferResponse
                {
                    Success = false,
                    Message = "Transfer not found"
                };
            }

            // Verify the user has modify permission on both accounts (owner or Manager role)
            if (!await _accountAccessService.CanModifyAccountAsync(request.UserId, transfer.SourceAccountId))
            {
                throw new UnauthorizedAccessException("You do not have permission to modify transfers on the source account.");
            }
            if (!await _accountAccessService.CanModifyAccountAsync(request.UserId, transfer.DestinationAccountId))
            {
                throw new UnauthorizedAccessException("You do not have permission to modify transfers on the destination account.");
            }

            // Get the related transactions
            var transactions = await _transactionRepository.GetByTransferIdAsync(request.TransferId, request.UserId);
            if (transactions.Count != 2)
            {
                return new ReverseTransferResponse
                {
                    Success = false,
                    Message = "Invalid transfer - expected 2 transactions"
                };
            }

            // Find source and destination transactions
            var sourceTransaction = transactions.FirstOrDefault(t => t.IsTransferSource);
            var destTransaction = transactions.FirstOrDefault(t => !t.IsTransferSource);

            if (sourceTransaction == null || destTransaction == null)
            {
                return new ReverseTransferResponse
                {
                    Success = false,
                    Message = "Invalid transfer structure"
                };
            }

            // Swap the account IDs in the transfer entity
            var oldSourceAccountId = transfer.SourceAccountId;
            var oldDestAccountId = transfer.DestinationAccountId;
            
            transfer.SourceAccountId = oldDestAccountId;
            transfer.DestinationAccountId = oldSourceAccountId;
            transfer.UpdatedAt = DateTime.UtcNow;

            // Update the transfer
            await _transferRepository.UpdateAsync(transfer);

            // Swap the IsTransferSource flags on the transactions
            sourceTransaction.IsTransferSource = false;
            sourceTransaction.UpdatedAt = DateTime.UtcNow;
            
            destTransaction.IsTransferSource = true;
            destTransaction.UpdatedAt = DateTime.UtcNow;

            // Update the transactions
            await _transactionRepository.UpdateAsync(sourceTransaction);
            await _transactionRepository.UpdateAsync(destTransaction);

            // Save all changes
            await _transferRepository.SaveChangesAsync();

            return new ReverseTransferResponse
            {
                Success = true,
                Message = "Transfer direction reversed successfully",
                NewSourceAccount = destTransaction.Account?.Name ?? "Unknown",
                NewDestinationAccount = sourceTransaction.Account?.Name ?? "Unknown"
            };
        }
        catch (Exception ex)
        {
            return new ReverseTransferResponse
            {
                Success = false,
                Message = $"Failed to reverse transfer: {ex.Message}"
            };
        }
    }
}