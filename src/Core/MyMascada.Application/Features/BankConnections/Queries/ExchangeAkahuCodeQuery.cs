using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.DTOs;

namespace MyMascada.Application.Features.BankConnections.Queries;

/// <summary>
/// Query to exchange an OAuth code for Akahu accounts.
/// This is used after the OAuth callback to get available accounts before linking.
/// NOTE: This is for Production App OAuth mode only, not Personal App mode.
/// The appIdToken must be provided by the caller (from configuration).
/// </summary>
public record ExchangeAkahuCodeQuery(
    Guid UserId,
    string Code,
    string State,
    string AppIdToken
) : IRequest<ExchangeAkahuCodeResult>;

/// <summary>
/// Result of the OAuth code exchange, containing available accounts and the access token.
/// </summary>
public record ExchangeAkahuCodeResult(
    IEnumerable<AkahuAccountDto> Accounts,
    string AccessToken
);

/// <summary>
/// Handler for exchanging OAuth code and getting available accounts.
/// NOTE: This handler is for Production App OAuth mode. For Personal App mode,
/// credentials are stored per-user in AkahuUserCredential.
/// </summary>
public class ExchangeAkahuCodeQueryHandler : IRequestHandler<ExchangeAkahuCodeQuery, ExchangeAkahuCodeResult>
{
    private const string AkahuProviderId = "akahu";

    private readonly IAkahuApiClient _akahuApiClient;
    private readonly IBankConnectionRepository _bankConnectionRepository;
    private readonly IApplicationLogger<ExchangeAkahuCodeQueryHandler> _logger;

    public ExchangeAkahuCodeQueryHandler(
        IAkahuApiClient akahuApiClient,
        IBankConnectionRepository bankConnectionRepository,
        IApplicationLogger<ExchangeAkahuCodeQueryHandler> logger)
    {
        _akahuApiClient = akahuApiClient ?? throw new ArgumentNullException(nameof(akahuApiClient));
        _bankConnectionRepository = bankConnectionRepository ?? throw new ArgumentNullException(nameof(bankConnectionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ExchangeAkahuCodeResult> Handle(ExchangeAkahuCodeQuery request, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Exchanging OAuth code for user {UserId}",
            request.UserId);

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            throw new ArgumentException("Authorization code is required");
        }

        if (string.IsNullOrWhiteSpace(request.State))
        {
            throw new ArgumentException("State parameter is required");
        }

        if (string.IsNullOrWhiteSpace(request.AppIdToken))
        {
            throw new ArgumentException("AppIdToken is required for OAuth mode");
        }

        // 1. Exchange code for access token
        var tokenResponse = await _akahuApiClient.ExchangeCodeForTokenAsync(request.Code, cancellationToken);

        if (string.IsNullOrEmpty(tokenResponse.AccessToken))
        {
            throw new InvalidOperationException("Failed to obtain access token from Akahu");
        }

        // 2. Get all Akahu accounts using the app's token and the OAuth access token
        var akahuAccounts = await _akahuApiClient.GetAccountsWithCredentialsAsync(
            request.AppIdToken,
            tokenResponse.AccessToken,
            cancellationToken);

        // 3. Get existing Akahu connections for this user to check which are already linked
        var existingConnections = await _bankConnectionRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        var linkedExternalIds = existingConnections
            .Where(c => c.ProviderId == AkahuProviderId && !string.IsNullOrEmpty(c.ExternalAccountId))
            .Select(c => c.ExternalAccountId!)
            .ToHashSet();

        // 4. Map to DTOs and mark already linked accounts
        var accountDtos = akahuAccounts.Select(account => new AkahuAccountDto
        {
            Id = account.Id,
            Name = account.Name,
            FormattedAccount = account.FormattedAccount,
            Type = account.Type,
            BankName = account.BankName,
            CurrentBalance = account.CurrentBalance,
            Currency = account.Currency,
            IsAlreadyLinked = linkedExternalIds.Contains(account.Id)
        }).ToList();

        _logger.LogDebug(
            "Found {TotalCount} Akahu accounts, {LinkedCount} already linked for user {UserId}",
            accountDtos.Count, accountDtos.Count(a => a.IsAlreadyLinked), request.UserId);

        return new ExchangeAkahuCodeResult(accountDtos, tokenResponse.AccessToken);
    }
}
