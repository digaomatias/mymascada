using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.BankConnections.Commands;

/// <summary>
/// Command to complete linking an Akahu account to a MyMascada account.
/// Uses the user's stored Akahu credentials to verify the account.
/// </summary>
public record CompleteAkahuConnectionCommand(
    Guid UserId,
    int AccountId,
    string AkahuAccountId
) : IRequest<BankConnectionDto>;

/// <summary>
/// Handler for completing Akahu account linking.
/// </summary>
public class CompleteAkahuConnectionCommandHandler : IRequestHandler<CompleteAkahuConnectionCommand, BankConnectionDto>
{
    private const string AkahuProviderId = "akahu";

    private readonly IAkahuApiClient _akahuApiClient;
    private readonly IAkahuUserCredentialRepository _credentialRepository;
    private readonly IBankConnectionRepository _bankConnectionRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ISettingsEncryptionService _encryptionService;
    private readonly IBankProviderFactory _providerFactory;
    private readonly IApplicationLogger<CompleteAkahuConnectionCommandHandler> _logger;

    public CompleteAkahuConnectionCommandHandler(
        IAkahuApiClient akahuApiClient,
        IAkahuUserCredentialRepository credentialRepository,
        IBankConnectionRepository bankConnectionRepository,
        IAccountRepository accountRepository,
        ISettingsEncryptionService encryptionService,
        IBankProviderFactory providerFactory,
        IApplicationLogger<CompleteAkahuConnectionCommandHandler> logger)
    {
        _akahuApiClient = akahuApiClient ?? throw new ArgumentNullException(nameof(akahuApiClient));
        _credentialRepository = credentialRepository ?? throw new ArgumentNullException(nameof(credentialRepository));
        _bankConnectionRepository = bankConnectionRepository ?? throw new ArgumentNullException(nameof(bankConnectionRepository));
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BankConnectionDto> Handle(CompleteAkahuConnectionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Completing Akahu connection for user {UserId}, account {AccountId}, Akahu account {AkahuAccountId}",
            request.UserId, request.AccountId, request.AkahuAccountId);

        // 1. Get user's Akahu credentials
        var credential = await _credentialRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (credential == null)
        {
            _logger.LogWarning("User {UserId} has no Akahu credentials", request.UserId);
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
            // Decryption failed - Data Protection keys may have changed
            _logger.LogWarning(ex, "Failed to decrypt Akahu credentials for user {UserId}", request.UserId);
            throw new InvalidOperationException(
                "Your stored credentials could not be decrypted. Please re-enter your Akahu tokens in the Bank Connections settings.", ex);
        }

        // 2. Verify the MyMascada account exists and belongs to the user
        var account = await _accountRepository.GetByIdAsync(request.AccountId, request.UserId);
        if (account == null)
        {
            _logger.LogWarning(
                "Account {AccountId} not found or does not belong to user {UserId}",
                request.AccountId, request.UserId);
            throw new ArgumentException($"Account with ID {request.AccountId} not found or does not belong to user");
        }

        // 3. Check if this Akahu account is already linked by the SAME user
        // Different users can link the same Akahu account (e.g., for testing or shared accounts)
        var existingConnection = await _bankConnectionRepository.GetByExternalAccountIdAsync(
            request.AkahuAccountId, AkahuProviderId, cancellationToken);
        if (existingConnection != null && existingConnection.UserId == request.UserId)
        {
            _logger.LogWarning(
                "Akahu account {AkahuAccountId} is already linked by this user to connection {ConnectionId}",
                request.AkahuAccountId, existingConnection.Id);
            throw new InvalidOperationException($"This Akahu account is already linked to another one of your MyMascada accounts");
        }
        else if (existingConnection != null)
        {
            _logger.LogInformation(
                "Akahu account {AkahuAccountId} is linked by another user (connection {ConnectionId}), allowing duplicate link for user {UserId}",
                request.AkahuAccountId, existingConnection.Id, request.UserId);
        }

        // 4. Check if the MyMascada account already has a bank connection
        var existingAccountConnection = await _bankConnectionRepository.GetByAccountIdAsync(request.AccountId, cancellationToken);
        if (existingAccountConnection != null)
        {
            _logger.LogWarning(
                "Account {AccountId} already has a bank connection {ConnectionId}",
                request.AccountId, existingAccountConnection.Id);
            throw new InvalidOperationException($"This account already has a bank connection. Disconnect it first.");
        }

        // 5. Verify the Akahu account exists using the user's credentials
        _logger.LogDebug("Verifying Akahu account {AkahuAccountId}", request.AkahuAccountId);
        AkahuAccountInfo? akahuAccount;
        try
        {
            akahuAccount = await _akahuApiClient.GetAccountWithCredentialsAsync(
                appIdToken, userToken, request.AkahuAccountId, cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Akahu credentials invalid when verifying account for user {UserId}", request.UserId);
            throw new InvalidOperationException("Your Akahu credentials are no longer valid. Please re-enter your tokens.", ex);
        }

        if (akahuAccount == null)
        {
            _logger.LogWarning("Akahu account {AkahuAccountId} not found", request.AkahuAccountId);
            throw new ArgumentException($"Akahu account {request.AkahuAccountId} not found");
        }

        // 6. Create connection settings (only sync state, tokens are per-user)
        var settings = new AkahuConnectionSettings
        {
            AkahuAccountId = request.AkahuAccountId
        };
        var encryptedSettings = _encryptionService.EncryptSettings(settings);

        // 7. Create the bank connection entity
        var bankConnection = new BankConnection
        {
            AccountId = request.AccountId,
            UserId = request.UserId,
            ProviderId = AkahuProviderId,
            EncryptedSettings = encryptedSettings,
            ExternalAccountId = request.AkahuAccountId,
            ExternalAccountName = akahuAccount.Name,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var savedConnection = await _bankConnectionRepository.AddAsync(bankConnection, cancellationToken);

        _logger.LogInformation(
            "Created bank connection {ConnectionId} for user {UserId}, linking account {AccountId} to Akahu {AkahuAccountId}",
            savedConnection.Id, request.UserId, request.AccountId, request.AkahuAccountId);

        // 8. Get provider display name
        var provider = _providerFactory.GetProviderOrDefault(AkahuProviderId);
        var providerName = provider?.DisplayName ?? "Akahu (NZ Banks)";

        return new BankConnectionDto
        {
            Id = savedConnection.Id,
            AccountId = savedConnection.AccountId,
            AccountName = account.Name,
            ProviderId = savedConnection.ProviderId,
            ProviderName = providerName,
            ExternalAccountId = savedConnection.ExternalAccountId,
            ExternalAccountName = savedConnection.ExternalAccountName,
            IsActive = savedConnection.IsActive,
            LastSyncAt = savedConnection.LastSyncAt,
            LastSyncError = savedConnection.LastSyncError,
            CreatedAt = savedConnection.CreatedAt
        };
    }

    /// <summary>
    /// Settings class for Akahu connections stored in encrypted settings.
    /// NOTE: Access tokens are now stored per-user in AkahuUserCredential, not here.
    /// This only stores sync state and the specific Akahu account ID for this connection.
    /// </summary>
    private class AkahuConnectionSettings
    {
        public string AkahuAccountId { get; set; } = string.Empty;
        public string? LastSyncedTransactionId { get; set; }
        public DateTime? LastSyncTimestamp { get; set; }
    }
}
