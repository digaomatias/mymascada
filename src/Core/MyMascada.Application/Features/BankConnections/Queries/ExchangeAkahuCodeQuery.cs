using MediatR;
using MyMascada.Application.BackgroundJobs;
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
    string? State,
    string AppIdToken
) : IRequest<ExchangeAkahuCodeResult>;

/// <summary>
/// Result of the OAuth code exchange, containing available accounts.
/// The access token is persisted server-side only and never returned to the client.
/// </summary>
public record ExchangeAkahuCodeResult(
    IEnumerable<AkahuAccountDto> Accounts
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
    private readonly IAkahuUserCredentialRepository _credentialRepository;
    private readonly IBankConnectionRepository _bankConnectionRepository;
    private readonly ISettingsEncryptionService _encryptionService;
    private readonly IOAuthStateStore _oauthStateStore;
    private readonly IAkahuWebhookSubscriptionService _webhookSubscriptionService;
    private readonly IAkahuMigrationJobService _migrationJobService;
    private readonly IApplicationLogger<ExchangeAkahuCodeQueryHandler> _logger;

    public ExchangeAkahuCodeQueryHandler(
        IAkahuApiClient akahuApiClient,
        IAkahuUserCredentialRepository credentialRepository,
        IBankConnectionRepository bankConnectionRepository,
        ISettingsEncryptionService encryptionService,
        IOAuthStateStore oauthStateStore,
        IAkahuWebhookSubscriptionService webhookSubscriptionService,
        IAkahuMigrationJobService migrationJobService,
        IApplicationLogger<ExchangeAkahuCodeQueryHandler> logger)
    {
        _akahuApiClient = akahuApiClient ?? throw new ArgumentNullException(nameof(akahuApiClient));
        _credentialRepository = credentialRepository ?? throw new ArgumentNullException(nameof(credentialRepository));
        _bankConnectionRepository = bankConnectionRepository ?? throw new ArgumentNullException(nameof(bankConnectionRepository));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        _oauthStateStore = oauthStateStore ?? throw new ArgumentNullException(nameof(oauthStateStore));
        _webhookSubscriptionService = webhookSubscriptionService ?? throw new ArgumentNullException(nameof(webhookSubscriptionService));
        _migrationJobService = migrationJobService ?? throw new ArgumentNullException(nameof(migrationJobService));
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

        if (string.IsNullOrWhiteSpace(request.AppIdToken))
        {
            throw new ArgumentException("AppIdToken is required for OAuth mode");
        }

        // Validate OAuth state server-side (CSRF protection)
        if (string.IsNullOrWhiteSpace(request.State))
        {
            throw new ArgumentException("OAuth state parameter is required");
        }

        var stateValid = await _oauthStateStore.ValidateAndConsumeAsync(request.UserId, request.State, cancellationToken);
        if (!stateValid)
        {
            throw new UnauthorizedAccessException("Invalid or expired OAuth state. Please restart the connection flow.");
        }

        // 1. Exchange code for access token
        var tokenResponse = await _akahuApiClient.ExchangeCodeForTokenAsync(request.Code, cancellationToken);

        if (string.IsNullOrEmpty(tokenResponse.AccessToken))
        {
            throw new InvalidOperationException("Failed to obtain access token from Akahu");
        }

        // Persist the OAuth token so the existing account-linking and sync flows
        // can use the same per-user credential store as personal-token mode.
        var encryptedAppToken = _encryptionService.EncryptSettings(request.AppIdToken);
        var encryptedUserToken = _encryptionService.EncryptSettings(tokenResponse.AccessToken);
        var existingCredential = await _credentialRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        if (existingCredential != null)
        {
            existingCredential.EncryptedAppToken = encryptedAppToken;
            existingCredential.EncryptedUserToken = encryptedUserToken;
            existingCredential.LastValidatedAt = DateTime.UtcNow;
            existingCredential.LastValidationError = null;
            existingCredential.ConsentScope = tokenResponse.Scope;
            existingCredential.ConsentGrantedAt = DateTimeOffset.UtcNow;
            existingCredential.ConsentCorrelationId = request.State;
            existingCredential.ConsentRevokedAt = null;
            existingCredential.UpdatedAt = DateTime.UtcNow;
            await _credentialRepository.UpdateAsync(existingCredential, cancellationToken);
        }
        else
        {
            await _credentialRepository.AddAsync(new MyMascada.Domain.Entities.AkahuUserCredential
            {
                UserId = request.UserId,
                EncryptedAppToken = encryptedAppToken,
                EncryptedUserToken = encryptedUserToken,
                LastValidatedAt = DateTime.UtcNow,
                ConsentScope = tokenResponse.Scope,
                ConsentGrantedAt = DateTimeOffset.UtcNow,
                ConsentCorrelationId = request.State,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }, cancellationToken);
        }

        // 1b. Make sure the user has the required Akahu webhook subscriptions in place. The
        // service swallows per-type failures so this never blocks OAuth completion.
        try
        {
            var ensureResult = await _webhookSubscriptionService.EnsureSubscriptionsAsync(request.UserId, cancellationToken);
            _logger.LogInformation(
                "Akahu webhook subscriptions ensured for user {UserId}: subscribed={SubscribedCount}, adopted={AdoptedCount}, healthy={HealthyCount}, failed={FailedCount}",
                request.UserId,
                ensureResult.SubscribedTypes.Count,
                ensureResult.AdoptedTypes.Count,
                ensureResult.AlreadyHealthyTypes.Count,
                ensureResult.FailedTypes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EnsureSubscriptionsAsync threw for user {UserId} during OAuth callback; continuing", request.UserId);
        }

        // 1c. Enqueue the Akahu classic→official migration as a background job for any
        // existing active connections the user has that have not already been migrated.
        // Failures are logged but never block the OAuth callback.
        try
        {
            var existingAkahuConnections = await _bankConnectionRepository.GetByUserIdAsync(request.UserId, cancellationToken);
            var candidates = existingAkahuConnections
                .Where(c => c.ProviderId == AkahuProviderId && c.IsActive && c.LastMigratedAt == null)
                .ToList();

            if (candidates.Count > 0)
            {
                foreach (var candidate in candidates)
                {
                    try
                    {
                        var jobId = _migrationJobService.EnqueueMigration(request.UserId, candidate.Id);
                        _logger.LogInformation(
                            "Post-OAuth: enqueued Akahu migration job {JobId} for connection {ConnectionId} (user {UserId})",
                            jobId, candidate.Id, request.UserId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Post-OAuth: failed to enqueue Akahu migration for connection {ConnectionId}", candidate.Id);
                    }
                }

                _logger.LogInformation(
                    "Post-OAuth Akahu migration sweep enqueued {Count} job(s) for user {UserId}",
                    candidates.Count, request.UserId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Post-OAuth Akahu migration sweep failed for user {UserId}; continuing", request.UserId);
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

        return new ExchangeAkahuCodeResult(accountDtos);
    }
}
