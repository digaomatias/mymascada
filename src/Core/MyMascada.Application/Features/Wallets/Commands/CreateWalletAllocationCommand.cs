using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Wallets.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Wallets.Commands;

public class CreateWalletAllocationCommand : IRequest<WalletAllocationDto>
{
    public int WalletId { get; set; }
    public int TransactionId { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
    public Guid UserId { get; set; }
}

public class CreateWalletAllocationCommandHandler : IRequestHandler<CreateWalletAllocationCommand, WalletAllocationDto>
{
    private readonly IWalletRepository _walletRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountAccessService _accountAccessService;

    public CreateWalletAllocationCommandHandler(
        IWalletRepository walletRepository,
        ITransactionRepository transactionRepository,
        IAccountAccessService accountAccessService)
    {
        _walletRepository = walletRepository;
        _transactionRepository = transactionRepository;
        _accountAccessService = accountAccessService;
    }

    public async Task<WalletAllocationDto> Handle(CreateWalletAllocationCommand request, CancellationToken cancellationToken)
    {
        // Verify wallet belongs to user
        var wallet = await _walletRepository.GetWalletByIdAsync(request.WalletId, request.UserId, cancellationToken);
        if (wallet == null)
        {
            throw new ArgumentException("Wallet not found or you don't have permission to access it.");
        }

        // Get the transaction to verify it exists and get its AccountId
        var transaction = await _transactionRepository.GetByIdAsync(request.TransactionId);
        if (transaction == null)
        {
            throw new ArgumentException("Transaction not found.");
        }

        // Verify user has access to the transaction's account
        var canAccess = await _accountAccessService.CanAccessAccountAsync(request.UserId, transaction.AccountId);
        if (!canAccess)
        {
            throw new ArgumentException("You don't have permission to access this transaction's account.");
        }

        if (request.Amount == 0)
        {
            throw new ArgumentException("Allocation amount cannot be zero.");
        }

        var allocation = new WalletAllocation
        {
            WalletId = request.WalletId,
            TransactionId = request.TransactionId,
            Amount = request.Amount,
            Note = request.Note?.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var createdAllocation = await _walletRepository.CreateAllocationAsync(allocation, cancellationToken);

        return new WalletAllocationDto
        {
            Id = createdAllocation.Id,
            TransactionId = createdAllocation.TransactionId,
            TransactionDescription = createdAllocation.Transaction.GetDisplayDescription(),
            TransactionDate = createdAllocation.Transaction.TransactionDate,
            AccountName = createdAllocation.Transaction.Account?.Name ?? string.Empty,
            Amount = createdAllocation.Amount,
            Note = createdAllocation.Note,
            CreatedAt = createdAllocation.CreatedAt
        };
    }
}
