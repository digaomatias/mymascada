using MediatR;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.BankConnections.Queries;

/// <summary>
/// Query returning the user's Akahu connections that still need to be migrated to the
/// official open-banking equivalent before the 24 May 2026 cut-over.
/// </summary>
public record GetAkahuMigrationStatusQuery(Guid UserId) : IRequest<AkahuMigrationStatusDto>;

/// <summary>
/// DTO describing the user's pending Akahu migrations and the global deadline.
/// </summary>
public record AkahuMigrationStatusDto(
    IReadOnlyList<PendingMigrationConnectionDto> PendingConnections,
    DateTimeOffset Deadline);

/// <summary>
/// One row per Akahu <see cref="MyMascada.Domain.Entities.BankConnection"/> that still
/// resolves to a classic <c>acc_xxx</c> and has a discoverable <c>_migrated</c> successor.
/// </summary>
public record PendingMigrationConnectionDto(
    int ConnectionId,
    string? BankName,
    string? ExternalAccountId,
    DateTime? LastSyncedAt);

/// <summary>
/// Handler for <see cref="GetAkahuMigrationStatusQuery"/>. Tolerates token errors and returns
/// an empty list rather than throwing, so the frontend dashboard can call this without guarding.
/// </summary>
public class GetAkahuMigrationStatusQueryHandler
    : IRequestHandler<GetAkahuMigrationStatusQuery, AkahuMigrationStatusDto>
{
    private const string AkahuProviderId = "akahu";

    /// <summary>
    /// Marker the migration command writes into <c>BankConnection.LastSyncError</c> when it
    /// cannot find a matching <c>_migrated</c> account and disables the connection. The
    /// migration banner needs to keep showing those rows so the user can re-authorise.
    /// </summary>
    private const string AwaitingReauthMarker = "Awaiting re-authorisation";

    /// <summary>
    /// 24 May 2026, 23:59:59 New Zealand time (UTC+12).
    /// </summary>
    private static readonly DateTimeOffset MigrationDeadline =
        new(2026, 5, 24, 23, 59, 59, TimeSpan.FromHours(12));

    private readonly IBankConnectionRepository _bankConnectionRepository;
    private readonly IAkahuUserCredentialRepository _credentialRepository;
    private readonly IAkahuApiClient _akahuApiClient;
    private readonly ISettingsEncryptionService _encryptionService;
    private readonly IApplicationLogger<GetAkahuMigrationStatusQueryHandler> _logger;

    public GetAkahuMigrationStatusQueryHandler(
        IBankConnectionRepository bankConnectionRepository,
        IAkahuUserCredentialRepository credentialRepository,
        IAkahuApiClient akahuApiClient,
        ISettingsEncryptionService encryptionService,
        IApplicationLogger<GetAkahuMigrationStatusQueryHandler> logger)
    {
        _bankConnectionRepository = bankConnectionRepository ?? throw new ArgumentNullException(nameof(bankConnectionRepository));
        _credentialRepository = credentialRepository ?? throw new ArgumentNullException(nameof(credentialRepository));
        _akahuApiClient = akahuApiClient ?? throw new ArgumentNullException(nameof(akahuApiClient));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AkahuMigrationStatusDto> Handle(GetAkahuMigrationStatusQuery request, CancellationToken cancellationToken)
    {
        var empty = new AkahuMigrationStatusDto(Array.Empty<PendingMigrationConnectionDto>(), MigrationDeadline);

        var allConnections = await _bankConnectionRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        var akahuConnections = allConnections
            .Where(c => c.ProviderId == AkahuProviderId
                && !string.IsNullOrEmpty(c.ExternalAccountId)
                && c.LastMigratedAt == null
                && (c.IsActive
                    || (c.LastSyncError != null && c.LastSyncError.Contains(AwaitingReauthMarker, StringComparison.OrdinalIgnoreCase))))
            .ToList();

        if (akahuConnections.Count == 0)
            return empty;

        var credential = await _credentialRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (credential == null)
            return empty;

        string appIdToken;
        string userToken;
        try
        {
            var decryptedApp = _encryptionService.DecryptSettings<string>(credential.EncryptedAppToken);
            var decryptedUser = _encryptionService.DecryptSettings<string>(credential.EncryptedUserToken);

            if (string.IsNullOrEmpty(decryptedApp) || string.IsNullOrEmpty(decryptedUser))
                return empty;

            appIdToken = decryptedApp;
            userToken = decryptedUser;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetAkahuMigrationStatus: failed to decrypt credentials for user {UserId}", request.UserId);
            return empty;
        }

        IReadOnlyList<AkahuAccountInfo> accounts;
        try
        {
            accounts = await _akahuApiClient.GetAccountsWithCredentialsAsync(appIdToken, userToken, cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "GetAkahuMigrationStatus: Akahu credentials unauthorised for user {UserId}", request.UserId);
            return empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetAkahuMigrationStatus: Akahu API call failed for user {UserId}", request.UserId);
            return empty;
        }

        var migratedToOld = accounts
            .Where(a => !string.IsNullOrEmpty(a.Migrated))
            .GroupBy(a => a.Migrated!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var pending = new List<PendingMigrationConnectionDto>();
        foreach (var connection in akahuConnections)
        {
            if (string.IsNullOrEmpty(connection.ExternalAccountId))
                continue;

            if (!migratedToOld.ContainsKey(connection.ExternalAccountId))
                continue;

            pending.Add(new PendingMigrationConnectionDto(
                connection.Id,
                connection.ExternalAccountName,
                connection.ExternalAccountId,
                connection.LastSyncAt));
        }

        return new AkahuMigrationStatusDto(pending, MigrationDeadline);
    }
}
