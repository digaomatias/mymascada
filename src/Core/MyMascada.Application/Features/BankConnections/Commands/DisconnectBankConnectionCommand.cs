using MediatR;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.BankConnections.Commands;

/// <summary>
/// Command to disconnect and delete a bank connection.
/// Revokes the provider token and removes the connection from the database.
/// </summary>
public record DisconnectBankConnectionCommand(
    Guid UserId,
    int BankConnectionId
) : IRequest<bool>;

/// <summary>
/// Handler for disconnecting a bank connection.
/// </summary>
public class DisconnectBankConnectionCommandHandler : IRequestHandler<DisconnectBankConnectionCommand, bool>
{
    private const string AkahuProviderId = "akahu";

    private readonly IBankConnectionRepository _bankConnectionRepository;
    private readonly IBankSyncLogRepository _syncLogRepository;
    private readonly IAkahuApiClient _akahuApiClient;
    private readonly IAkahuUserCredentialRepository _credentialRepository;
    private readonly ISettingsEncryptionService _encryptionService;
    private readonly IApplicationLogger<DisconnectBankConnectionCommandHandler> _logger;

    public DisconnectBankConnectionCommandHandler(
        IBankConnectionRepository bankConnectionRepository,
        IBankSyncLogRepository syncLogRepository,
        IAkahuApiClient akahuApiClient,
        IAkahuUserCredentialRepository credentialRepository,
        ISettingsEncryptionService encryptionService,
        IApplicationLogger<DisconnectBankConnectionCommandHandler> logger)
    {
        _bankConnectionRepository = bankConnectionRepository ?? throw new ArgumentNullException(nameof(bankConnectionRepository));
        _syncLogRepository = syncLogRepository ?? throw new ArgumentNullException(nameof(syncLogRepository));
        _akahuApiClient = akahuApiClient ?? throw new ArgumentNullException(nameof(akahuApiClient));
        _credentialRepository = credentialRepository ?? throw new ArgumentNullException(nameof(credentialRepository));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(DisconnectBankConnectionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Disconnecting bank connection {ConnectionId} for user {UserId}",
            request.BankConnectionId, request.UserId);

        // 1. Get the bank connection
        var connection = await _bankConnectionRepository.GetByIdAsync(request.BankConnectionId, cancellationToken);
        if (connection == null)
        {
            _logger.LogWarning(
                "Bank connection {ConnectionId} not found",
                request.BankConnectionId);
            throw new ArgumentException($"Bank connection {request.BankConnectionId} not found");
        }

        // 2. Verify ownership
        if (connection.UserId != request.UserId)
        {
            _logger.LogWarning(
                "User {UserId} does not own bank connection {ConnectionId} (owned by {OwnerId})",
                request.UserId, request.BankConnectionId, connection.UserId);
            throw new UnauthorizedAccessException("You do not have access to this bank connection");
        }

        // 3. Revoke the provider token if applicable.
        // Tokens are stored per-user in AkahuUserCredential, not per-connection.
        if (connection.ProviderId == AkahuProviderId)
        {
            try
            {
                var credential = await _credentialRepository.GetByUserIdAsync(request.UserId, cancellationToken);
                if (credential != null)
                {
                    var appIdToken = _encryptionService.DecryptSettings<string>(credential.EncryptedAppToken);
                    var accessToken = _encryptionService.DecryptSettings<string>(credential.EncryptedUserToken);

                    if (!string.IsNullOrEmpty(appIdToken) && !string.IsNullOrEmpty(accessToken))
                    {
                        _logger.LogDebug("Revoking Akahu access token for connection {ConnectionId}", request.BankConnectionId);
                        await _akahuApiClient.RevokeTokenAsync(appIdToken, accessToken, cancellationToken);
                        _logger.LogDebug("Successfully revoked Akahu access token");
                    }

                    // Record consent revocation timestamp for compliance evidence
                    credential.ConsentRevokedAt = DateTimeOffset.UtcNow;
                    credential.UpdatedAt = DateTime.UtcNow;
                    await _credentialRepository.UpdateAsync(credential, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                // Log but continue - we still want to delete the connection even if token revocation fails
                _logger.LogWarning(ex,
                    "Failed to revoke access token for connection {ConnectionId}. Continuing with deletion.",
                    request.BankConnectionId);
            }
        }

        // 4. Delete the bank connection (soft delete)
        await _bankConnectionRepository.DeleteAsync(request.BankConnectionId, cancellationToken);

        _logger.LogInformation(
            "Successfully disconnected bank connection {ConnectionId} for user {UserId}",
            request.BankConnectionId, request.UserId);

        return true;
    }
}
