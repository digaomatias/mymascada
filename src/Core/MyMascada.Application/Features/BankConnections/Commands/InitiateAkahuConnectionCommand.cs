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
    private readonly IApplicationLogger<InitiateAkahuConnectionCommandHandler> _logger;

    public InitiateAkahuConnectionCommandHandler(
        IAkahuApiClient akahuApiClient,
        IAkahuUserCredentialRepository credentialRepository,
        IBankConnectionRepository bankConnectionRepository,
        ISettingsEncryptionService encryptionService,
        IApplicationLogger<InitiateAkahuConnectionCommandHandler> logger)
    {
        _akahuApiClient = akahuApiClient ?? throw new ArgumentNullException(nameof(akahuApiClient));
        _credentialRepository = credentialRepository ?? throw new ArgumentNullException(nameof(credentialRepository));
        _bankConnectionRepository = bankConnectionRepository ?? throw new ArgumentNullException(nameof(bankConnectionRepository));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<InitiateConnectionResult> Handle(InitiateAkahuConnectionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initiating Akahu connection for user {UserId}", request.UserId);

        // Check if user has stored credentials
        var credential = await _credentialRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        if (credential == null)
        {
            // User needs to set up credentials first
            _logger.LogInformation("User {UserId} has no Akahu credentials - setup required", request.UserId);
            return new InitiateConnectionResult
            {
                RequiresCredentials = true,
                IsPersonalAppMode = true // We only support Personal App mode
            };
        }

        // User has credentials - decrypt them and fetch accounts
        _logger.LogInformation("User {UserId} has Akahu credentials - fetching available accounts", request.UserId);

        string? appIdToken;
        string? userToken;
        try
        {
            appIdToken = _encryptionService.DecryptSettings<string>(credential.EncryptedAppToken);
            userToken = _encryptionService.DecryptSettings<string>(credential.EncryptedUserToken);
        }
        catch (Exception ex)
        {
            // Decryption failed - Data Protection keys may have changed (e.g., after deployment)
            // Mark credentials as needing re-setup
            _logger.LogWarning(ex, "Failed to decrypt Akahu credentials for user {UserId} - keys may have changed", request.UserId);

            credential.LastValidationError = "Credentials could not be decrypted. Please re-enter your tokens.";
            credential.UpdatedAt = DateTime.UtcNow;
            await _credentialRepository.UpdateAsync(credential, cancellationToken);

            return new InitiateConnectionResult
            {
                RequiresCredentials = true,
                IsPersonalAppMode = true,
                CredentialsError = "Your stored credentials could not be decrypted. This can happen after system updates. Please re-enter your App Token and User Token."
            };
        }

        IReadOnlyList<AkahuAccountInfo> accounts;
        try
        {
            accounts = await _akahuApiClient.GetAccountsWithCredentialsAsync(appIdToken, userToken, cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            // Credentials may have been revoked - mark as needing re-setup
            _logger.LogWarning(ex, "Akahu credentials invalid for user {UserId}", request.UserId);

            // Update credential with validation error
            credential.LastValidationError = "Credentials are no longer valid. Please re-enter your tokens.";
            credential.UpdatedAt = DateTime.UtcNow;
            await _credentialRepository.UpdateAsync(credential, cancellationToken);

            return new InitiateConnectionResult
            {
                RequiresCredentials = true,
                IsPersonalAppMode = true,
                CredentialsError = "Your Akahu credentials are no longer valid. Please re-enter your App Token and User Token."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Akahu accounts for user {UserId}", request.UserId);
            throw new InvalidOperationException("Failed to connect to Akahu. Please try again later.", ex);
        }

        // Get existing connections to mark already linked accounts
        var existingConnections = await _bankConnectionRepository.GetByUserIdAsync(request.UserId, cancellationToken);
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

        // Update last validated timestamp
        credential.LastValidatedAt = DateTime.UtcNow;
        credential.LastValidationError = null;
        credential.UpdatedAt = DateTime.UtcNow;
        await _credentialRepository.UpdateAsync(credential, cancellationToken);

        return new InitiateConnectionResult
        {
            IsPersonalAppMode = true,
            RequiresCredentials = false,
            AvailableAccounts = accountDtos
        };
    }
}
