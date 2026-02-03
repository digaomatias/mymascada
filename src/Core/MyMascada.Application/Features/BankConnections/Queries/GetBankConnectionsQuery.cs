using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.DTOs;

namespace MyMascada.Application.Features.BankConnections.Queries;

/// <summary>
/// Query to get all bank connections for a user.
/// </summary>
public record GetBankConnectionsQuery(
    Guid UserId
) : IRequest<IEnumerable<BankConnectionDto>>;

/// <summary>
/// Handler for listing user's bank connections.
/// </summary>
public class GetBankConnectionsQueryHandler : IRequestHandler<GetBankConnectionsQuery, IEnumerable<BankConnectionDto>>
{
    private readonly IBankConnectionRepository _bankConnectionRepository;
    private readonly IBankProviderFactory _providerFactory;
    private readonly IApplicationLogger<GetBankConnectionsQueryHandler> _logger;

    public GetBankConnectionsQueryHandler(
        IBankConnectionRepository bankConnectionRepository,
        IBankProviderFactory providerFactory,
        IApplicationLogger<GetBankConnectionsQueryHandler> logger)
    {
        _bankConnectionRepository = bankConnectionRepository ?? throw new ArgumentNullException(nameof(bankConnectionRepository));
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<BankConnectionDto>> Handle(GetBankConnectionsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Fetching bank connections for user {UserId}",
            request.UserId);

        var connections = await _bankConnectionRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        var connectionDtos = connections.Select(c =>
        {
            var provider = _providerFactory.GetProviderOrDefault(c.ProviderId);
            return new BankConnectionDto
            {
                Id = c.Id,
                AccountId = c.AccountId,
                AccountName = c.Account?.Name ?? "Unknown Account",
                ProviderId = c.ProviderId,
                ProviderName = provider?.DisplayName ?? c.ProviderId,
                ExternalAccountId = c.ExternalAccountId,
                ExternalAccountName = c.ExternalAccountName,
                IsActive = c.IsActive,
                LastSyncAt = c.LastSyncAt,
                LastSyncError = c.LastSyncError,
                CreatedAt = c.CreatedAt
            };
        }).ToList();

        _logger.LogDebug(
            "Found {Count} bank connections for user {UserId}",
            connectionDtos.Count, request.UserId);

        return connectionDtos;
    }
}
