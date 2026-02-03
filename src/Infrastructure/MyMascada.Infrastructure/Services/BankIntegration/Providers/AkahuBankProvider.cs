using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.DTOs;

namespace MyMascada.Infrastructure.Services.BankIntegration.Providers;

/// <summary>
/// Bank provider implementation for Akahu (NZ bank aggregator).
/// Credentials are stored per-user in AkahuUserCredential, not per-connection.
/// </summary>
public class AkahuBankProvider : IBankProvider
{
    private readonly AkahuApiClient _apiClient;
    private readonly IAkahuUserCredentialRepository _credentialRepository;
    private readonly ISettingsEncryptionService _encryptionService;
    private readonly IApplicationLogger<AkahuBankProvider> _logger;

    public string ProviderId => "akahu";
    public string DisplayName => "Akahu (NZ Banks)";
    public bool SupportsWebhooks => true;
    public bool SupportsBalanceFetch => true;

    public AkahuBankProvider(
        AkahuApiClient apiClient,
        IAkahuUserCredentialRepository credentialRepository,
        ISettingsEncryptionService encryptionService,
        IApplicationLogger<AkahuBankProvider> logger)
    {
        _apiClient = apiClient;
        _credentialRepository = credentialRepository;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task<BankConnectionTestResult> TestConnectionAsync(BankConnectionConfig config, CancellationToken ct = default)
    {
        try
        {
            var (appIdToken, userToken, credentialError) = await GetUserCredentialsAsync(config.UserId, ct);
            if (credentialError != null)
            {
                return BankConnectionTestResult.Failure(credentialError);
            }

            var settings = GetSettings(config);
            var accountId = settings?.AkahuAccountId ?? config.ExternalAccountId;
            if (string.IsNullOrEmpty(accountId))
            {
                return BankConnectionTestResult.Failure("No Akahu account ID configured");
            }

            var account = await _apiClient.GetAccountInternalAsync(appIdToken!, userToken!, accountId, ct);
            if (account == null)
            {
                return BankConnectionTestResult.Failure($"Account {settings.AkahuAccountId} not found");
            }

            if (account.Status != "ACTIVE")
            {
                return BankConnectionTestResult.Failure($"Account status is {account.Status}, not ACTIVE");
            }

            return BankConnectionTestResult.Success(account.Id, account.Name);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Akahu connection test failed - unauthorized");
            return BankConnectionTestResult.Failure("Token expired or revoked. Please update your Akahu credentials.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Akahu connection test failed");
            return BankConnectionTestResult.Failure($"Connection test failed: {ex.Message}");
        }
    }

    public async Task<BankTransactionFetchResult> FetchTransactionsAsync(
        BankConnectionConfig config,
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        try
        {
            var (appIdToken, userToken, credentialError) = await GetUserCredentialsAsync(config.UserId, ct);
            if (credentialError != null)
            {
                return BankTransactionFetchResult.Failure(credentialError);
            }

            var settings = GetSettings(config);
            var accountId = settings?.AkahuAccountId ?? config.ExternalAccountId;
            if (string.IsNullOrEmpty(accountId))
            {
                return BankTransactionFetchResult.Failure("No Akahu account ID configured");
            }

            var transactions = await _apiClient.GetTransactionsAsync(
                appIdToken!,
                userToken!,
                accountId,
                from,
                to,
                ct);

            _logger.LogInformation("Fetched {Count} transactions from Akahu for account {AccountId} ({From:yyyy-MM-dd} to {To:yyyy-MM-dd})",
                transactions.Count, accountId, from, to);

            var mapped = transactions.Select(MapTransaction).ToList();
            return BankTransactionFetchResult.Success(mapped);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Akahu fetch transactions failed - unauthorized");
            return BankTransactionFetchResult.Failure("Token expired or revoked. Please update your Akahu credentials.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Akahu fetch transactions failed");
            return BankTransactionFetchResult.Failure($"Failed to fetch transactions: {ex.Message}");
        }
    }

    public async Task<BankBalanceResult?> FetchBalanceAsync(BankConnectionConfig config, CancellationToken ct = default)
    {
        try
        {
            var (appIdToken, userToken, credentialError) = await GetUserCredentialsAsync(config.UserId, ct);
            if (credentialError != null)
            {
                return BankBalanceResult.Failure(credentialError);
            }

            var settings = GetSettings(config);
            var accountId = settings?.AkahuAccountId ?? config.ExternalAccountId;
            if (string.IsNullOrEmpty(accountId))
            {
                return BankBalanceResult.Failure("No Akahu account ID configured");
            }

            var account = await _apiClient.GetAccountInternalAsync(appIdToken!, userToken!, accountId, ct);
            if (account?.Balance == null)
            {
                return BankBalanceResult.Failure("Could not retrieve account balance");
            }

            return BankBalanceResult.Success(
                account.Balance.Current,
                account.Balance.Available,
                account.Balance.Currency,
                account.Balance.UpdatedAt ?? DateTime.UtcNow);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Akahu fetch balance failed - unauthorized");
            return BankBalanceResult.Failure("Token expired or revoked. Please update your Akahu credentials.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Akahu fetch balance failed");
            return BankBalanceResult.Failure($"Failed to fetch balance: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the user's Akahu credentials from the database.
    /// Returns (appIdToken, userToken, errorMessage). If errorMessage is non-null, credentials failed.
    /// </summary>
    private async Task<(string? AppIdToken, string? UserToken, string? ErrorMessage)> GetUserCredentialsAsync(
        Guid userId,
        CancellationToken ct)
    {
        var credential = await _credentialRepository.GetByUserIdAsync(userId, ct);
        if (credential == null)
        {
            return (null, null, "No Akahu credentials configured. Please set up your Akahu credentials first.");
        }

        try
        {
            var appIdToken = _encryptionService.DecryptSettings<string>(credential.EncryptedAppToken);
            var userToken = _encryptionService.DecryptSettings<string>(credential.EncryptedUserToken);

            if (string.IsNullOrEmpty(appIdToken) || string.IsNullOrEmpty(userToken))
            {
                return (null, null, "Invalid Akahu credentials. Please re-enter your tokens.");
            }

            return (appIdToken, userToken, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt Akahu credentials for user {UserId}", userId);
            return (null, null, "Failed to decrypt credentials. Please re-enter your Akahu tokens.");
        }
    }

    private AkahuConnectionSettings? GetSettings(BankConnectionConfig config)
    {
        if (!config.Settings.TryGetValue("encrypted", out var encrypted) || encrypted is not string encryptedStr)
        {
            return null;
        }
        return _encryptionService.DecryptSettings<AkahuConnectionSettings>(encryptedStr);
    }

    private static BankTransactionDto MapTransaction(AkahuTransaction tx)
    {
        // Build reference from NZ payment metadata
        var referenceParts = new[] { tx.Meta?.Particulars, tx.Meta?.Code, tx.Meta?.Reference }
            .Where(p => !string.IsNullOrEmpty(p));
        var reference = referenceParts.Any() ? string.Join(" | ", referenceParts) : null;

        // Build metadata dictionary
        var metadata = new Dictionary<string, object>();
        if (tx.Meta?.OtherAccount != null)
            metadata["otherAccount"] = tx.Meta.OtherAccount;
        if (tx.Meta?.CardSuffix != null)
            metadata["cardSuffix"] = tx.Meta.CardSuffix;
        if (tx.Meta?.Conversion != null)
        {
            metadata["foreignAmount"] = tx.Meta.Conversion.Amount;
            metadata["foreignCurrency"] = tx.Meta.Conversion.Currency;
            metadata["exchangeRate"] = tx.Meta.Conversion.Rate;
        }
        if (tx.Category?.Groups?.PersonalFinance != null)
            metadata["akahuCategoryGroup"] = tx.Category.Groups.PersonalFinance;

        return new BankTransactionDto
        {
            ExternalId = tx.Id,
            Date = tx.Date,
            Amount = tx.Amount,  // Akahu uses standard convention: negative = expense
            Description = tx.Merchant?.Name ?? tx.Description,
            Reference = reference,
            Category = tx.Category?.Name,
            MerchantName = tx.Merchant?.Name,
            Metadata = metadata.Count > 0 ? metadata : null
        };
    }
}
