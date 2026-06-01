using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.DTOs;

namespace MyMascada.Application.Features.BankConnections.Commands;

/// <summary>
/// Command to migrate a MyMascada bank connection from the user's Akahu classic account
/// (<c>acc_xxx</c>) to the equivalent official open-banking account that Akahu created during
/// the 2026-05 classic-to-official migration. The handler updates
/// <see cref="MyMascada.Domain.Entities.BankConnection.ExternalAccountId"/>, the encrypted
/// <c>AkahuConnectionSettings.AkahuAccountId</c>, and rewrites historical
/// <see cref="MyMascada.Domain.Entities.Transaction.ExternalId"/> values for the account so
/// that the existing exact-match dedup pipeline keeps catching duplicate Akahu re-imports.
/// </summary>
public record MigrateAkahuConnectionCommand(Guid UserId, int BankConnectionId) : IRequest<MigrateAkahuConnectionResult>;

/// <summary>
/// Result of <see cref="MigrateAkahuConnectionCommand"/>.
/// </summary>
public record MigrateAkahuConnectionResult(
    bool Success,
    string? OldExternalAccountId,
    string? NewExternalAccountId,
    int TransactionsRemapped,
    string? ErrorMessage);

public class MigrateAkahuConnectionCommandHandler
    : IRequestHandler<MigrateAkahuConnectionCommand, MigrateAkahuConnectionResult>
{
    private const string AkahuProviderId = "akahu";
    private const int MaxMigrationLookbackDays = 365;

    private readonly IBankConnectionRepository _bankConnectionRepository;
    private readonly IAkahuUserCredentialRepository _credentialRepository;
    private readonly IAkahuApiClient _akahuApiClient;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ISettingsEncryptionService _encryptionService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationLogger<MigrateAkahuConnectionCommandHandler> _logger;

    public MigrateAkahuConnectionCommandHandler(
        IBankConnectionRepository bankConnectionRepository,
        IAkahuUserCredentialRepository credentialRepository,
        IAkahuApiClient akahuApiClient,
        ITransactionRepository transactionRepository,
        ISettingsEncryptionService encryptionService,
        IUnitOfWork unitOfWork,
        IApplicationLogger<MigrateAkahuConnectionCommandHandler> logger)
    {
        _bankConnectionRepository = bankConnectionRepository ?? throw new ArgumentNullException(nameof(bankConnectionRepository));
        _credentialRepository = credentialRepository ?? throw new ArgumentNullException(nameof(credentialRepository));
        _akahuApiClient = akahuApiClient ?? throw new ArgumentNullException(nameof(akahuApiClient));
        _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MigrateAkahuConnectionResult> Handle(MigrateAkahuConnectionCommand request, CancellationToken cancellationToken)
    {
        var connection = await _bankConnectionRepository.GetByIdAsync(request.BankConnectionId, cancellationToken);
        if (connection == null || connection.ProviderId != AkahuProviderId)
        {
            _logger.LogWarning("Migrate Akahu connection: BankConnection {ConnectionId} not found or not an Akahu connection", request.BankConnectionId);
            return new MigrateAkahuConnectionResult(false, null, null, 0, "Bank connection not found");
        }

        if (connection.UserId != request.UserId)
        {
            _logger.LogWarning(
                "Migrate Akahu connection: BankConnection {ConnectionId} belongs to user {OwnerId}, not requesting user {RequestUserId}",
                connection.Id, connection.UserId, request.UserId);
            return new MigrateAkahuConnectionResult(false, connection.ExternalAccountId, null, 0, "Bank connection not found");
        }

        var oldExternalAccountId = connection.ExternalAccountId;
        if (string.IsNullOrEmpty(oldExternalAccountId))
        {
            _logger.LogWarning("Migrate Akahu connection {ConnectionId}: no ExternalAccountId set, nothing to migrate", connection.Id);
            return new MigrateAkahuConnectionResult(false, null, null, 0, "Bank connection has no external account ID");
        }

        var credential = await _credentialRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (credential == null)
        {
            _logger.LogWarning("Migrate Akahu connection {ConnectionId}: no Akahu credentials for user {UserId}", connection.Id, request.UserId);
            return new MigrateAkahuConnectionResult(false, oldExternalAccountId, null, 0, "Akahu credentials not found for user");
        }

        string appIdToken;
        string userToken;
        try
        {
            var decryptedApp = _encryptionService.DecryptSettings<string>(credential.EncryptedAppToken);
            var decryptedUser = _encryptionService.DecryptSettings<string>(credential.EncryptedUserToken);
            if (string.IsNullOrEmpty(decryptedApp) || string.IsNullOrEmpty(decryptedUser))
            {
                _logger.LogWarning("Migrate Akahu connection {ConnectionId}: decrypted Akahu tokens were empty", connection.Id);
                return new MigrateAkahuConnectionResult(false, oldExternalAccountId, null, 0, "Akahu credentials could not be decrypted");
            }
            appIdToken = decryptedApp;
            userToken = decryptedUser;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migrate Akahu connection {ConnectionId}: failed to decrypt Akahu credentials", connection.Id);
            return new MigrateAkahuConnectionResult(false, oldExternalAccountId, null, 0, "Akahu credentials could not be decrypted");
        }

        // Decrypt EncryptedSettings up front so we fail fast (before any expensive Akahu
        // API calls) if Data Protection keys have rotated and rendered the blob unreadable.
        AkahuConnectionSettings existingSettings;
        if (!string.IsNullOrEmpty(connection.EncryptedSettings))
        {
            try
            {
                existingSettings = _encryptionService.DecryptSettings<AkahuConnectionSettings>(connection.EncryptedSettings)
                    ?? new AkahuConnectionSettings();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Migrate Akahu connection {ConnectionId}: failed to decrypt EncryptedSettings", connection.Id);
                connection.IsActive = false;
                connection.LastSyncError = "Settings could not be decrypted; please re-link this account.";
                connection.UpdatedAt = DateTime.UtcNow;
                await _bankConnectionRepository.UpdateAsync(connection, cancellationToken);
                return new MigrateAkahuConnectionResult(false, oldExternalAccountId, null, 0, "Connection settings could not be decrypted");
            }
        }
        else
        {
            existingSettings = new AkahuConnectionSettings();
        }

        IReadOnlyList<AkahuAccountInfo> accounts;
        try
        {
            accounts = await _akahuApiClient.GetAccountsWithCredentialsAsync(appIdToken, userToken, cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Migrate Akahu connection {ConnectionId}: Akahu credentials unauthorised", connection.Id);
            return new MigrateAkahuConnectionResult(false, oldExternalAccountId, null, 0, "Akahu credentials are no longer valid");
        }

        var migratedAccount = accounts.FirstOrDefault(a =>
            !string.IsNullOrEmpty(a.Migrated) && a.Migrated == oldExternalAccountId);

        if (migratedAccount == null)
        {
            _logger.LogWarning(
                "Migrate Akahu connection {ConnectionId}: no account in Akahu carries _migrated={OldAccountId}; marking awaiting re-auth",
                connection.Id, oldExternalAccountId);

            connection.IsActive = false;
            connection.LastSyncError = "Awaiting re-authorisation";
            connection.UpdatedAt = DateTime.UtcNow;
            await _bankConnectionRepository.UpdateAsync(connection, cancellationToken);

            return new MigrateAkahuConnectionResult(
                false,
                oldExternalAccountId,
                null,
                0,
                "No migrated Akahu account found — user must re-authorise");
        }

        var newAccountId = migratedAccount.Id;

        var oldestDate = await _transactionRepository.GetOldestTransactionDateForAccountAsync(connection.AccountId, cancellationToken);
        var today = DateTime.UtcNow.Date;
        var earliest = today.AddDays(-MaxMigrationLookbackDays);
        var fromDate = oldestDate.HasValue && oldestDate.Value.Date > earliest
            ? oldestDate.Value.Date
            : earliest;
        if (fromDate > today)
        {
            fromDate = earliest;
        }

        var newTransactions = await _akahuApiClient.GetTransactionsWithCredentialsAsync(
            appIdToken, userToken, newAccountId, fromDate, today, cancellationToken);

        var idMap = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var tx in newTransactions)
        {
            if (!string.IsNullOrEmpty(tx.Migrated) && !string.IsNullOrEmpty(tx.ExternalId) && tx.Migrated != tx.ExternalId)
            {
                idMap[tx.Migrated] = tx.ExternalId;
            }
        }

        connection.ExternalAccountId = newAccountId;
        if (!string.IsNullOrEmpty(migratedAccount.Name))
        {
            connection.ExternalAccountName = migratedAccount.Name;
        }
        connection.IsActive = true;
        connection.LastSyncError = null;
        connection.LastMigratedAt = DateTime.UtcNow;
        connection.UpdatedAt = DateTime.UtcNow;

        existingSettings.AkahuAccountId = newAccountId;
        connection.EncryptedSettings = _encryptionService.EncryptSettings(existingSettings);

        // Wrap the connection update and transaction remap in a single DB transaction so a
        // failure during remap leaves the connection still pointing at the old ExternalAccountId
        // (which the next sync will retry) rather than at the new one with stale transaction rows.
        var remapped = 0;
        await using (var tx = await _unitOfWork.BeginTransactionAsync(cancellationToken))
        {
            await _bankConnectionRepository.UpdateAsync(connection, cancellationToken);

            if (idMap.Count > 0)
            {
                remapped = await _transactionRepository.RemapExternalIdsAsync(connection.AccountId, idMap, cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
        }

        // TODO(akahu-migration follow-up PR): rewrite trans_xxx values embedded in
        // ReconciliationItem.BankReferenceData JSON and trigger a fresh BankSyncService run
        // for this connection so post-migration deltas are picked up. Tracked in
        // docs/plans/akahu-migration-impact.md §4.3 steps 10-11.

        _logger.LogInformation(
            "Akahu connection migrated. User={UserId}, ConnectionId={ConnectionId}, Old={OldAccountId}, New={NewAccountId}, TxRemapped={TransactionsRemapped}",
            request.UserId, connection.Id, oldExternalAccountId, newAccountId, remapped);

        return new MigrateAkahuConnectionResult(
            true,
            oldExternalAccountId,
            newAccountId,
            remapped,
            null);
    }
}
