using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Wallets.DTOs;
using MyMascada.Application.Features.Wallets.Mappers;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Wallets.Commands;

public class UpdateWalletCommand : IRequest<WalletDetailDto>
{
    public int WalletId { get; set; }
    public string? Name { get; set; }
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public string? Currency { get; set; }
    public bool? IsArchived { get; set; }
    public decimal? TargetAmount { get; set; }
    public bool ClearTargetAmount { get; set; }
    public Guid UserId { get; set; }
}

public class UpdateWalletCommandHandler : IRequestHandler<UpdateWalletCommand, WalletDetailDto>
{
    private readonly IWalletRepository _walletRepository;

    public UpdateWalletCommandHandler(IWalletRepository walletRepository)
    {
        _walletRepository = walletRepository;
    }

    public async Task<WalletDetailDto> Handle(UpdateWalletCommand request, CancellationToken cancellationToken)
    {
        var wallet = await _walletRepository.GetWalletByIdAsync(request.WalletId, request.UserId, cancellationToken);
        if (wallet == null)
        {
            throw new ArgumentException("Wallet not found or you don't have permission to access it.");
        }

        if (request.Name != null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ArgumentException("Wallet name cannot be empty.");
            }

            if (await _walletRepository.WalletNameExistsAsync(request.UserId, request.Name.Trim(), request.WalletId, cancellationToken))
            {
                throw new ArgumentException($"A wallet with the name '{request.Name.Trim()}' already exists.");
            }

            wallet.Name = request.Name.Trim();
        }

        if (request.Icon != null)
        {
            wallet.Icon = request.Icon.Trim();
        }

        if (request.Color != null)
        {
            wallet.Color = request.Color.Trim();
        }

        // Fix #4: Validate currency is a 3-letter code
        if (request.Currency != null)
        {
            var currency = request.Currency.Trim().ToUpperInvariant();
            if (currency.Length != 3 || !currency.All(char.IsLetter))
                throw new ArgumentException("Currency must be a 3-letter code.");

            if (currency != wallet.Currency)
            {
                // Block currency changes on wallets that have existing allocations
                var allocations = await _walletRepository.GetAllocationsForWalletAsync(request.WalletId, cancellationToken);
                if (allocations.Any())
                    throw new ArgumentException("Cannot change currency on a wallet that has existing allocations.");
            }

            wallet.Currency = currency;
        }

        if (request.IsArchived.HasValue)
        {
            wallet.IsArchived = request.IsArchived.Value;
        }

        // Fix #5: Allow clearing TargetAmount via ClearTargetAmount flag
        if (request.ClearTargetAmount)
        {
            wallet.TargetAmount = null;
        }
        else if (request.TargetAmount.HasValue)
        {
            wallet.TargetAmount = request.TargetAmount.Value;
        }

        var updatedWallet = await _walletRepository.UpdateWalletAsync(wallet, cancellationToken);
        var balance = await _walletRepository.GetWalletBalanceAsync(updatedWallet.Id, cancellationToken);

        return WalletMapper.ToDetailDto(updatedWallet, balance);
    }
}
