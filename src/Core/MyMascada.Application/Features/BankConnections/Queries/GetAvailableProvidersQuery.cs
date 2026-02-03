using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.DTOs;

namespace MyMascada.Application.Features.BankConnections.Queries;

/// <summary>
/// Query to get available bank providers.
/// </summary>
public record GetAvailableProvidersQuery() : IRequest<IEnumerable<BankProviderInfo>>;

/// <summary>
/// Handler for listing available bank providers.
/// </summary>
public class GetAvailableProvidersQueryHandler : IRequestHandler<GetAvailableProvidersQuery, IEnumerable<BankProviderInfo>>
{
    private readonly IBankProviderFactory _providerFactory;
    private readonly IApplicationLogger<GetAvailableProvidersQueryHandler> _logger;

    public GetAvailableProvidersQueryHandler(
        IBankProviderFactory providerFactory,
        IApplicationLogger<GetAvailableProvidersQueryHandler> logger)
    {
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IEnumerable<BankProviderInfo>> Handle(GetAvailableProvidersQuery request, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Fetching available bank providers");

        var providers = _providerFactory.GetAvailableProviders();

        _logger.LogDebug("Found {Count} available bank providers", providers.Count);

        return Task.FromResult(providers.AsEnumerable());
    }
}
