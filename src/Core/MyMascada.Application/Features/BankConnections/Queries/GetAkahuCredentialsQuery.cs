using MediatR;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.BankConnections.Queries;

/// <summary>
/// Query to get a user's Akahu credentials (decrypted).
/// For internal use by other handlers - credentials should not be exposed via API.
/// </summary>
public record GetAkahuCredentialsQuery(Guid UserId) : IRequest<AkahuCredentials?>;

/// <summary>
/// Decrypted Akahu credentials for a user.
/// </summary>
public record AkahuCredentials
{
    public string AppIdToken { get; init; } = string.Empty;
    public string UserToken { get; init; } = string.Empty;
    public DateTime? LastValidatedAt { get; init; }
}

/// <summary>
/// Handler for getting Akahu credentials.
/// </summary>
public class GetAkahuCredentialsQueryHandler : IRequestHandler<GetAkahuCredentialsQuery, AkahuCredentials?>
{
    private readonly IAkahuUserCredentialRepository _credentialRepository;
    private readonly ISettingsEncryptionService _encryptionService;
    private readonly IApplicationLogger<GetAkahuCredentialsQueryHandler> _logger;

    public GetAkahuCredentialsQueryHandler(
        IAkahuUserCredentialRepository credentialRepository,
        ISettingsEncryptionService encryptionService,
        IApplicationLogger<GetAkahuCredentialsQueryHandler> logger)
    {
        _credentialRepository = credentialRepository;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task<AkahuCredentials?> Handle(GetAkahuCredentialsQuery request, CancellationToken cancellationToken)
    {
        var credential = await _credentialRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        if (credential == null)
        {
            _logger.LogDebug("No Akahu credentials found for user {UserId}", request.UserId);
            return null;
        }

        // Decrypt the credentials
        string? appIdToken;
        string? userToken;
        try
        {
            appIdToken = _encryptionService.DecryptSettings<string>(credential.EncryptedAppToken);
            userToken = _encryptionService.DecryptSettings<string>(credential.EncryptedUserToken);
        }
        catch (Exception ex)
        {
            // Decryption failed - Data Protection keys may have changed
            _logger.LogWarning(ex, "Failed to decrypt Akahu credentials for user {UserId} - credentials need re-entry", request.UserId);
            return null;
        }

        return new AkahuCredentials
        {
            AppIdToken = appIdToken,
            UserToken = userToken,
            LastValidatedAt = credential.LastValidatedAt
        };
    }
}
