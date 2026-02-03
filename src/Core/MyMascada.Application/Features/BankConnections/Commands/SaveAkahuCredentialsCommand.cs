using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.BankConnections.Commands;

/// <summary>
/// Command to save or update a user's Akahu credentials.
/// Validates the credentials against the Akahu API before saving.
/// </summary>
public record SaveAkahuCredentialsCommand(
    Guid UserId,
    string AppIdToken,
    string UserToken
) : IRequest<SaveAkahuCredentialsResult>;

/// <summary>
/// Result of saving Akahu credentials.
/// </summary>
public record SaveAkahuCredentialsResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<AkahuAccountDto>? AvailableAccounts { get; init; }
}

/// <summary>
/// Handler for saving Akahu credentials.
/// </summary>
public class SaveAkahuCredentialsCommandHandler : IRequestHandler<SaveAkahuCredentialsCommand, SaveAkahuCredentialsResult>
{
    private readonly IAkahuApiClient _akahuApiClient;
    private readonly IAkahuUserCredentialRepository _credentialRepository;
    private readonly IBankConnectionRepository _bankConnectionRepository;
    private readonly ISettingsEncryptionService _encryptionService;
    private readonly IApplicationLogger<SaveAkahuCredentialsCommandHandler> _logger;

    public SaveAkahuCredentialsCommandHandler(
        IAkahuApiClient akahuApiClient,
        IAkahuUserCredentialRepository credentialRepository,
        IBankConnectionRepository bankConnectionRepository,
        ISettingsEncryptionService encryptionService,
        IApplicationLogger<SaveAkahuCredentialsCommandHandler> logger)
    {
        _akahuApiClient = akahuApiClient;
        _credentialRepository = credentialRepository;
        _bankConnectionRepository = bankConnectionRepository;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task<SaveAkahuCredentialsResult> Handle(SaveAkahuCredentialsCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Validating and saving Akahu credentials for user {UserId}", request.UserId);

        // 1. Validate credentials by trying to fetch accounts
        IReadOnlyList<AkahuAccountInfo> accounts;
        try
        {
            accounts = await _akahuApiClient.GetAccountsWithCredentialsAsync(
                request.AppIdToken,
                request.UserToken,
                cancellationToken);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Invalid Akahu credentials for user {UserId}", request.UserId);
            return new SaveAkahuCredentialsResult
            {
                IsSuccess = false,
                ErrorMessage = "Invalid credentials. Please check your App Token and User Token."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate Akahu credentials for user {UserId}", request.UserId);
            return new SaveAkahuCredentialsResult
            {
                IsSuccess = false,
                ErrorMessage = "Failed to connect to Akahu. Please try again later."
            };
        }

        // 2. Encrypt the credentials
        var encryptedAppToken = _encryptionService.EncryptSettings(request.AppIdToken);
        var encryptedUserToken = _encryptionService.EncryptSettings(request.UserToken);

        // 3. Check if user already has credentials
        var existingCredential = await _credentialRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        if (existingCredential != null)
        {
            // Update existing credentials
            existingCredential.EncryptedAppToken = encryptedAppToken;
            existingCredential.EncryptedUserToken = encryptedUserToken;
            existingCredential.LastValidatedAt = DateTime.UtcNow;
            existingCredential.LastValidationError = null;
            existingCredential.UpdatedAt = DateTime.UtcNow;
            await _credentialRepository.UpdateAsync(existingCredential, cancellationToken);
            _logger.LogInformation("Updated Akahu credentials for user {UserId}", request.UserId);
        }
        else
        {
            // Create new credentials
            var newCredential = new AkahuUserCredential
            {
                UserId = request.UserId,
                EncryptedAppToken = encryptedAppToken,
                EncryptedUserToken = encryptedUserToken,
                LastValidatedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _credentialRepository.AddAsync(newCredential, cancellationToken);
            _logger.LogInformation("Created new Akahu credentials for user {UserId}", request.UserId);
        }

        // 4. Get existing connections to mark already linked accounts
        var existingConnections = await _bankConnectionRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        var linkedExternalIds = existingConnections
            .Where(c => c.ProviderId == "akahu")
            .Select(c => c.ExternalAccountId)
            .ToHashSet();

        // 5. Map accounts to DTOs
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
        }).ToList();

        return new SaveAkahuCredentialsResult
        {
            IsSuccess = true,
            AvailableAccounts = accountDtos
        };
    }
}
