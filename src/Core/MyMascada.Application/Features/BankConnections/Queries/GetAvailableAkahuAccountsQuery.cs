using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.DTOs;

namespace MyMascada.Application.Features.BankConnections.Queries;

/// <summary>
/// Query to get available Akahu accounts that are not yet linked.
/// Uses the user's stored Akahu credentials.
/// </summary>
public record GetAvailableAkahuAccountsQuery(Guid UserId) : IRequest<IEnumerable<AkahuAccountDto>>;

/// <summary>
/// Handler for listing available Akahu accounts.
/// </summary>
public class GetAvailableAkahuAccountsQueryHandler : IRequestHandler<GetAvailableAkahuAccountsQuery, IEnumerable<AkahuAccountDto>>
{
    private const string AkahuProviderId = "akahu";

    private readonly IAkahuApiClient _akahuApiClient;
    private readonly IAkahuUserCredentialRepository _credentialRepository;
    private readonly IBankConnectionRepository _bankConnectionRepository;
    private readonly ISettingsEncryptionService _encryptionService;
    private readonly IApplicationLogger<GetAvailableAkahuAccountsQueryHandler> _logger;

    public GetAvailableAkahuAccountsQueryHandler(
        IAkahuApiClient akahuApiClient,
        IAkahuUserCredentialRepository credentialRepository,
        IBankConnectionRepository bankConnectionRepository,
        ISettingsEncryptionService encryptionService,
        IApplicationLogger<GetAvailableAkahuAccountsQueryHandler> logger)
    {
        _akahuApiClient = akahuApiClient ?? throw new ArgumentNullException(nameof(akahuApiClient));
        _credentialRepository = credentialRepository ?? throw new ArgumentNullException(nameof(credentialRepository));
        _bankConnectionRepository = bankConnectionRepository ?? throw new ArgumentNullException(nameof(bankConnectionRepository));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<AkahuAccountDto>> Handle(GetAvailableAkahuAccountsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Fetching available Akahu accounts for user {UserId}", request.UserId);

        // 1. Get user's stored credentials
        var credential = await _credentialRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (credential == null)
        {
            throw new InvalidOperationException("Akahu credentials not configured. Please set up your Akahu credentials first.");
        }

        string? appIdToken;
        string? userToken;
        try
        {
            appIdToken = _encryptionService.DecryptSettings<string>(credential.EncryptedAppToken);
            userToken = _encryptionService.DecryptSettings<string>(credential.EncryptedUserToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decrypt Akahu credentials for user {UserId}", request.UserId);
            throw new InvalidOperationException(
                "Your stored credentials could not be decrypted. Please re-enter your Akahu tokens in the Bank Connections settings.", ex);
        }

        // 2. Get all Akahu accounts for the user
        var akahuAccounts = await _akahuApiClient.GetAccountsWithCredentialsAsync(appIdToken, userToken, cancellationToken);

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

        return accountDtos;
    }
}
