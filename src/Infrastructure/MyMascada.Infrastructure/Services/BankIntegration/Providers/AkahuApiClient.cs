using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Infrastructure.Services.BankIntegration.Providers;

/// <summary>
/// HTTP client wrapper for Akahu REST API.
/// For Personal App mode: Both App Token and User Token are provided per-user.
/// For Production App OAuth mode: App credentials from config, user token from OAuth.
/// </summary>
public class AkahuApiClient : IAkahuApiClient
{
    private readonly HttpClient _httpClient;
    private readonly AkahuOptions _options;
    private readonly IApplicationLogger<AkahuApiClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AkahuApiClient(
        HttpClient httpClient,
        IOptions<AkahuOptions> options,
        IApplicationLogger<AkahuApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_options.ApiBaseUrl);
        // Note: X-Akahu-Id header is now set per-request to support per-user credentials
    }

    /// <summary>
    /// Get OAuth authorization URL to redirect user to (Production App mode only)
    /// </summary>
    public string GetAuthorizationUrl(string state, string? email = null)
    {
        var scopes = string.Join(" ", _options.DefaultScopes);
        var url = $"{_options.OAuthBaseUrl}/authorize?client_id={_options.AppIdToken}&redirect_uri={Uri.EscapeDataString(_options.RedirectUri)}&response_type=code&scope={Uri.EscapeDataString(scopes)}&state={Uri.EscapeDataString(state)}";

        if (!string.IsNullOrEmpty(email))
            url += $"&email={Uri.EscapeDataString(email)}";

        return url;
    }

    /// <summary>
    /// Exchange authorization code for access token (Production App mode only)
    /// </summary>
    async Task<Application.Common.Interfaces.AkahuTokenResponse> IAkahuApiClient.ExchangeCodeForTokenAsync(string code, CancellationToken ct)
    {
        var response = await ExchangeCodeForTokenInternalAsync(code, ct);
        return new Application.Common.Interfaces.AkahuTokenResponse
        {
            AccessToken = response.AccessToken,
            TokenType = response.TokenType,
            Scope = response.Scope
        };
    }

    /// <summary>
    /// Get all accounts for the user using explicit credentials (interface implementation)
    /// </summary>
    public async Task<IReadOnlyList<AkahuAccountInfo>> GetAccountsWithCredentialsAsync(
        string appIdToken,
        string userToken,
        CancellationToken ct = default)
    {
        var accounts = await GetAccountsInternalAsync(appIdToken, userToken, ct);
        return accounts.Select(MapToAccountInfo).ToList();
    }

    /// <summary>
    /// Get a specific account using explicit credentials (interface implementation)
    /// </summary>
    public async Task<AkahuAccountInfo?> GetAccountWithCredentialsAsync(
        string appIdToken,
        string userToken,
        string accountId,
        CancellationToken ct = default)
    {
        var account = await GetAccountInternalAsync(appIdToken, userToken, accountId, ct);
        return account != null ? MapToAccountInfo(account) : null;
    }

    /// <summary>
    /// Validates that the provided credentials are valid by making a test API call.
    /// </summary>
    public async Task<bool> ValidateCredentialsAsync(
        string appIdToken,
        string userToken,
        CancellationToken ct = default)
    {
        try
        {
            // Try to fetch accounts - this will fail if credentials are invalid
            await GetAccountsInternalAsync(appIdToken, userToken, ct);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Unauthorized") || ex.Message.Contains("401"))
        {
            return false;
        }
    }

    private static AkahuAccountInfo MapToAccountInfo(AkahuAccount account) => new()
    {
        Id = account.Id,
        Name = account.Name,
        FormattedAccount = account.FormattedAccount,
        Type = account.Type,
        Status = account.Status,
        CurrentBalance = account.Balance?.Current,
        AvailableBalance = account.Balance?.Available,
        Currency = account.Balance?.Currency ?? "NZD",
        BankName = account.Connection?.Name ?? string.Empty
    };

    /// <summary>
    /// Exchange authorization code for access token (internal - Production App mode)
    /// </summary>
    public async Task<AkahuTokenResponse> ExchangeCodeForTokenInternalAsync(string code, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.OAuthBaseUrl}/token");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _options.RedirectUri,
            ["client_id"] = _options.AppIdToken,
            ["client_secret"] = _options.AppSecret
        });

        var response = await _httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response, "Token exchange", ct);

        return await response.Content.ReadFromJsonAsync<AkahuTokenResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Failed to parse token response");
    }

    /// <summary>
    /// Get all accounts for the user (internal - returns Akahu-specific types)
    /// </summary>
    public async Task<IReadOnlyList<AkahuAccount>> GetAccountsInternalAsync(
        string appIdToken,
        string userToken,
        CancellationToken ct = default)
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "accounts", appIdToken, userToken);
        _logger.LogInformation("Akahu API request: {Method} {BaseAddress}{RequestUri}",
            request.Method, _httpClient.BaseAddress, request.RequestUri);
        var response = await _httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response, "Get accounts", ct);

        var result = await response.Content.ReadFromJsonAsync<AkahuListResponse<AkahuAccount>>(JsonOptions, ct);
        return result?.Items ?? Array.Empty<AkahuAccount>();
    }

    /// <summary>
    /// Get a specific account (internal - returns Akahu-specific types)
    /// </summary>
    public async Task<AkahuAccount?> GetAccountInternalAsync(
        string appIdToken,
        string userToken,
        string accountId,
        CancellationToken ct = default)
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Get, $"accounts/{accountId}", appIdToken, userToken);
        var response = await _httpClient.SendAsync(request, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, "Get account", ct);

        var result = await response.Content.ReadFromJsonAsync<AkahuItemResponse<AkahuAccount>>(JsonOptions, ct);
        return result?.Item;
    }

    /// <summary>
    /// Get transactions for an account (with automatic pagination)
    /// </summary>
    public async Task<IReadOnlyList<AkahuTransaction>> GetTransactionsAsync(
        string appIdToken,
        string userToken,
        string accountId,
        DateTime? start = null,
        DateTime? end = null,
        CancellationToken ct = default)
    {
        var allTransactions = new List<AkahuTransaction>();
        string? cursor = null;
        var pageCount = 0;
        const int maxPages = 100; // Safety limit to prevent infinite loops

        do
        {
            var url = $"accounts/{accountId}/transactions";
            var queryParams = new List<string>();

            if (start.HasValue)
                queryParams.Add($"start={start.Value:yyyy-MM-dd}");
            if (end.HasValue)
                queryParams.Add($"end={end.Value:yyyy-MM-dd}");
            if (!string.IsNullOrEmpty(cursor))
                queryParams.Add($"cursor={cursor}");

            if (queryParams.Count > 0)
                url += "?" + string.Join("&", queryParams);

            var request = CreateAuthenticatedRequest(HttpMethod.Get, url, appIdToken, userToken);
            var response = await _httpClient.SendAsync(request, ct);
            await EnsureSuccessAsync(response, "Get transactions", ct);

            var result = await response.Content.ReadFromJsonAsync<AkahuListResponse<AkahuTransaction>>(JsonOptions, ct);

            if (result?.Items != null && result.Items.Length > 0)
            {
                allTransactions.AddRange(result.Items);
            }

            cursor = result?.Cursor?.Next;
            pageCount++;

            _logger.LogDebug("Fetched page {PageCount} with {Count} transactions, cursor: {HasMore}",
                pageCount, result?.Items?.Length ?? 0, cursor != null ? "more" : "done");

        } while (!string.IsNullOrEmpty(cursor) && pageCount < maxPages);

        if (pageCount >= maxPages)
        {
            _logger.LogWarning("Reached maximum page limit ({MaxPages}) while fetching transactions for account {AccountId}",
                maxPages, accountId);
        }

        _logger.LogInformation("Fetched {TotalCount} transactions across {PageCount} pages for account {AccountId}",
            allTransactions.Count, pageCount, accountId);

        return allTransactions;
    }

    /// <summary>
    /// Revoke user access token
    /// </summary>
    public async Task RevokeTokenAsync(string accessToken, CancellationToken ct = default)
    {
        // For revocation, we don't need the app token - just delete the token
        var request = new HttpRequestMessage(HttpMethod.Delete, "token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _httpClient.SendAsync(request, ct);
        // Don't throw on failure - token may already be revoked
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to revoke Akahu token: {StatusCode}", response.StatusCode);
        }
    }

    private HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string path, string appIdToken, string userToken)
    {
        // Build absolute URL to avoid HttpClient BaseAddress resolution issues
        var baseUrl = _options.ApiBaseUrl.TrimEnd('/');
        var fullUrl = $"{baseUrl}/{path.TrimStart('/')}";

        var request = new HttpRequestMessage(method, fullUrl);
        request.Headers.Add("X-Akahu-Id", appIdToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        return request;
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, string operation, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        var content = await response.Content.ReadAsStringAsync(ct);
        _logger.LogError(new HttpRequestException($"Akahu API error: {response.StatusCode}"),
            "Akahu API error - {Operation}: {StatusCode} - {Content}",
            operation, response.StatusCode, content);

        throw response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => new UnauthorizedAccessException($"Akahu: {operation} - Unauthorized. Token may be expired or revoked."),
            System.Net.HttpStatusCode.Forbidden => new UnauthorizedAccessException($"Akahu: {operation} - Forbidden. Insufficient permissions."),
            System.Net.HttpStatusCode.TooManyRequests => new InvalidOperationException($"Akahu: {operation} - Rate limit exceeded. Please try again later."),
            _ => new HttpRequestException($"Akahu: {operation} failed with status {response.StatusCode}: {content}")
        };
    }
}

// Akahu API response models
public record AkahuListResponse<T>
{
    public bool Success { get; init; }
    public T[] Items { get; init; } = Array.Empty<T>();
    public AkahuCursor? Cursor { get; init; }
}

public record AkahuCursor
{
    public string? Next { get; init; }
}

public record AkahuItemResponse<T>
{
    public bool Success { get; init; }
    public T? Item { get; init; }
}

public record AkahuTokenResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string TokenType { get; init; } = string.Empty;
    public string? Scope { get; init; }
}

public record AkahuAccount
{
    [JsonPropertyName("_id")]
    public string Id { get; init; } = string.Empty;  // acc_xxx
    public string Name { get; init; } = string.Empty;
    [JsonPropertyName("formatted_account")]
    public string FormattedAccount { get; init; } = string.Empty;  // 00-0000-0000000-00
    public string Type { get; init; } = string.Empty;  // CHECKING, SAVINGS, CREDITCARD
    public string Status { get; init; } = string.Empty;  // ACTIVE, INACTIVE
    public AkahuAccountBalance? Balance { get; init; }
    public AkahuConnection? Connection { get; init; }
    public string[] Attributes { get; init; } = Array.Empty<string>();
}

public record AkahuAccountBalance
{
    public decimal Current { get; init; }
    public decimal? Available { get; init; }
    public decimal? Limit { get; init; }
    public string Currency { get; init; } = "NZD";
    public DateTime? UpdatedAt { get; init; }
}

public record AkahuConnection
{
    [JsonPropertyName("_id")]
    public string Id { get; init; } = string.Empty;  // conn_xxx
    public string Name { get; init; } = string.Empty;  // e.g., "ANZ"
    public string? Logo { get; init; }
}

public record AkahuTransaction
{
    [JsonPropertyName("_id")]
    public string Id { get; init; } = string.Empty;  // trans_xxx
    [JsonPropertyName("_account")]
    public string AccountId { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal? Balance { get; init; }
    public string Type { get; init; } = string.Empty;  // CREDIT, DEBIT, etc.
    public AkahuTransactionCategory? Category { get; init; }
    public AkahuMerchant? Merchant { get; init; }
    public AkahuTransactionMeta? Meta { get; init; }
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }
}

public record AkahuTransactionCategory
{
    [JsonPropertyName("_id")]
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public AkahuCategoryGroups? Groups { get; init; }
}

public record AkahuCategoryGroups
{
    [JsonPropertyName("personal_finance")]
    public AkahuPersonalFinanceGroup? PersonalFinance { get; init; }
}

public record AkahuPersonalFinanceGroup
{
    [JsonPropertyName("_id")]
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public record AkahuMerchant
{
    [JsonPropertyName("_id")]
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Website { get; init; }
}

public record AkahuTransactionMeta
{
    public string? Particulars { get; init; }
    public string? Code { get; init; }
    public string? Reference { get; init; }
    public string? OtherAccount { get; init; }
    public string? CardSuffix { get; init; }
    public AkahuConversion? Conversion { get; init; }
    public string? Logo { get; init; }
}

public record AkahuConversion
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public decimal Rate { get; init; }
}
