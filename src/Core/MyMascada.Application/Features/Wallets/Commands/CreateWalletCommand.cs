using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Wallets.DTOs;
using MyMascada.Application.Features.Wallets.Mappers;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Wallets.Commands;

public class CreateWalletCommand : IRequest<WalletDetailDto>
{
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal? TargetAmount { get; set; }
    public Guid UserId { get; set; }
}

public class CreateWalletCommandHandler : IRequestHandler<CreateWalletCommand, WalletDetailDto>
{
    private readonly IWalletRepository _walletRepository;

    public CreateWalletCommandHandler(IWalletRepository walletRepository)
    {
        _walletRepository = walletRepository;
    }

    public async Task<WalletDetailDto> Handle(CreateWalletCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Wallet name is required.");
        }

        // Check for duplicate name
        if (await _walletRepository.WalletNameExistsAsync(request.UserId, request.Name.Trim(), ct: cancellationToken))
        {
            throw new ArgumentException($"A wallet with the name '{request.Name.Trim()}' already exists.");
        }

        var wallet = new Wallet
        {
            Name = request.Name.Trim(),
            Icon = request.Icon?.Trim(),
            Color = request.Color?.Trim(),
            Currency = request.Currency.Trim().ToUpperInvariant(),
            TargetAmount = request.TargetAmount,
            IsArchived = false,
            UserId = request.UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var createdWallet = await _walletRepository.CreateWalletAsync(wallet, cancellationToken);

        return WalletMapper.ToDetailDto(createdWallet, 0m);
    }
}
