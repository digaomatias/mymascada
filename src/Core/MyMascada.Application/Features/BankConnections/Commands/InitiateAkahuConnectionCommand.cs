using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.DTOs;

namespace MyMascada.Application.Features.BankConnections.Commands;

/// <summary>
/// Command to initiate the Akahu connection flow.
/// Checks if user has stored credentials and returns available accounts if so.
/// If no credentials, indicates that credentials setup is required first.
/// </summary>
public record InitiateAkahuConnectionCommand(
    Guid UserId,
    string? Email = null
) : IRequest<InitiateConnectionResult>;

/// <summary>
/// Handler for initiating Akahu connection flow.
/// </summary>
public class InitiateAkahuConnectionCommandHandler : IRequestHandler<InitiateAkahuConnectionCommand, InitiateConnectionResult>
{
    private readonly IAkahuApiClient _akahuApiClient;
    private readonly IAkahuUserCredentialRepository _credentialRepository;
    private readonly IBankConnectionRepository _bankConnectionRepository;
    private readonly ISettingsEncryptionService _encryptionService;
    private readonly IBankProviderModeResolver _modeResolver;
    private readonly IOAuthStateStore _oauthStateStore;
    private readonly IApplicationLogger<InitiateAkahuConnectionCommandHandler> _logger;

    public InitiateAkahuConnectionCommandHandler(
        IAkahuApiClient akahuApiClient,
        IAkahuUserCredentialRepository credentialRepository,
        IBankConnectionRepository bankConnectionRepository,
        ISettingsEncryptionService encryptionService,
        IBankProviderModeResolver modeResolver,
        IOAuthStateStore oauthStateStore,
        IApplicationLogger<InitiateAkahuConnectionCommandHandler> logger)
    {
        _akahuApiClient = akahuApiClient ?? throw new ArgumentNullException(nameof(akahuApiClient));
        _credentialRepository = credentialRepository ?? throw new ArgumentNullException(nameof(credentialRepository));
        _bankConnectionRepository = bankConnectionRepository ?? throw new ArgumentNullException(nameof(bankConnectionRepository));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        _modeResolver = modeResolver ?? throw new ArgumentNullException(nameof(modeResolver));
        _oauthStateStore = oauthStateStore ?? throw new ArgumentNullException(nameof(oauthStateStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<InitiateConnectionResult> Handle(InitiateAkahuConnectionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initiating Akahu connection for user {UserId}", request.UserId);

        var mode = _modeResolver.Resolve("akahu");

        // If the user already has valid (non-revoked) credentials, fetch accounts directly
        // instead of forcing another OAuth round-trip. This applies to both hosted_oauth
        // and personal_app modes — once authorized, the user shouldn't have to re-authorize
        // just to link an additional account.
        var credential = await _credentialRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        var hasUsableCredential = credential != null && credential.ConsentRevokedAt == null;

        if (hasUsableCredential)
        {
            var reuseResult = await TryReuseExistingCredentialsAsync(credential!, request.UserId, mode.DefaultMode, cancellationToken);
            if (reuseResult != null)
            {
                return reuseResult;
            }
            // Token couldn't be decrypted or was rejected by Akahu — fall through to
            // a fresh authorization flow appropriate to the configured mode.
        }

        if (mode.DefaultMode == "hosted_oauth")
        {
            var state = Guid.NewGuid().ToString("N");
            await _oauthStateStore.StoreAsync(request.UserId, state, cancellationToken);
            var authorizationUrl = _akahuApiClient.GetAuthorizationUrl(state, request.Email);

            return new InitiateConnectionResult
            {
                IsPersonalAppMode = false,
                RequiresCredentials = false,
                State = state,
                AuthorizationUrl = authorizationUrl
            };
        }

        _logger.LogInformation("User {UserId} has no Akahu credentials - setup required", request.UserId);
        return new InitiateConnectionResult
        {
            RequiresCredentials = true,
            IsPersonalAppMode = true
        };
    }

    /// <summary>
    /// Attempts to fetch the user's available Akahu accounts using their existing stored credentials.
    /// Returns null if the credentials are unusable (decryption failure or rejected by Akahu) — the
    /// caller should then fall back to a fresh authorization flow.
    /// </summary>
    private async Task<InitiateConnectionResult?> TryReuseExistingCredentialsAsync(
        Domain.Entities.AkahuUserCredential credential,
        Guid userId,
        string mode,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("User {UserId} has Akahu credentials - fetching available accounts without re-auth", userId);

        string? appIdToken;
        string? userToken;
        try
        {
            appIdToken = _encryptionService.DecryptSettings<string>(credential.EncryptedAppToken);
            userToken = _encryptionService.DecryptSettings<string>(credential.EncryptedUserToken);
        }
        catch (Exception ex)
        {
            // Decryption failed - Data Protection keys may have changed (e.g., after deployment).
            _logger.LogWarning(ex, "Failed to decrypt Akahu credentials for user {UserId} - keys may have changed", userId);
            credential.LastValidationError = "Credentials could not be decrypted. Please re-enter your tokens.";
            credential.UpdatedAt = DateTime.UtcNow;
            await _credentialRepository.UpdateAsync(credential, cancellationToken);

            if (mode == "personal_tokens")
            {
                return new InitiateConnectionResult
                {
                    RequiresCredentials = true,
                    IsPersonalAppMode = true,
                    CredentialsError = "Your stored credentials could not be decrypted. This can happen after system updates. Please re-enter your App Token and User Token."
                };
            }
            return null; // OAuth mode → caller will produce a fresh authorization URL.
        }

        IReadOnlyList<AkahuAccountInfo> accounts;
        try
        {
            accounts = await _akahuApiClient.GetAccountsWithCredentialsAsync(appIdToken, userToken, cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            // Tokens have been revoked or expired upstream.
            _logger.LogWarning(ex, "Akahu credentials invalid for user {UserId}", userId);
            credential.LastValidationError = "Credentials are no longer valid. Please re-authorize.";
            credential.UpdatedAt = DateTime.UtcNow;
            await _credentialRepository.UpdateAsync(credential, cancellationToken);

            if (mode == "personal_tokens")
            {
                return new InitiateConnectionResult
                {
                    RequiresCredentials = true,
                    IsPersonalAppMode = true,
                    CredentialsError = "Your Akahu credentials are no longer valid. Please re-enter your App Token and User Token."
                };
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Akahu accounts for user {UserId}", userId);
            throw new InvalidOperationException("Failed to connect to Akahu. Please try again later.", ex);
        }

        var existingConnections = await _bankConnectionRepository.GetByUserIdAsync(userId, cancellationToken);
        var linkedExternalIds = existingConnections
            .Where(c => c.ProviderId == "akahu")
            .Select(c => c.ExternalAccountId)
            .ToHashSet();

        var accountDtos = accounts.Select(a => new AkahuAccountDto
        {
            Id = a.Id,
            Name = a.Name,
            FormattedAccount = a.FormattedAccount,
            Type = a.Type,
            BankName = a.BankName,
            CurrentBalance = a.CurrentBalance,
            Currency = a.Currency,
            IsAlreadyLinked = linkedExternalIds.Contains(a.Id)
        });

        credential.LastValidatedAt = DateTime.UtcNow;
        credential.LastValidationError = null;
        credential.UpdatedAt = DateTime.UtcNow;
        await _credentialRepository.UpdateAsync(credential, cancellationToken);

        return new InitiateConnectionResult
        {
            // IsPersonalAppMode=true tells the frontend "use the inline accounts" — this
            // is the same shape the frontend already handles for personal-app mode, so
            // reusing it avoids a new code path on the client.
            IsPersonalAppMode = true,
            RequiresCredentials = false,
            AvailableAccounts = accountDtos
        };
    }
}
