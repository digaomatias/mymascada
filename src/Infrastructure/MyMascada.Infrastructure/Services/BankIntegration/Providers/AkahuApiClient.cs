using System.Net;
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
        var scopes = _options.DefaultScopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var effectiveScopes = scopes.Length > 0
            ? string.Join(" ", scopes)
            : "ENDURING_CONSENT";

        var uriBuilder = new UriBuilder(_options.OAuthBaseUrl.TrimEnd('/'))
        {
            Path = string.Empty,
            Query =
                $"client_id={Uri.EscapeDataString(_options.AppIdToken)}" +
                $"&redirect_uri={Uri.EscapeDataString(_options.RedirectUri)}" +
                "&response_type=code" +
                $"&scope={Uri.EscapeDataString(effectiveScopes)}" +
                $"&state={Uri.EscapeDataString(state)}"
        };

        if (!string.IsNullOrEmpty(email))
        {
            uriBuilder.Query += $"&email={Uri.EscapeDataString(email)}";
        }

        return uriBuilder.Uri.ToString();
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
        // Token exchange goes to the API base (e.g. api.akahu.io/v1/token),
        // NOT the OAuth consent page (oauth.akahu.nz).
        var baseUrl = _options.ApiBaseUrl.TrimEnd('/');
        var fullUrl = $"{baseUrl}/token";

        var request = new HttpRequestMessage(HttpMethod.Post, fullUrl);

        // Akahu requires Basic Auth: base64(app_token:app_secret)
        var credentials = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{_options.AppIdToken}:{_options.AppSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        // Akahu requires X-Akahu-Id header
        request.Headers.Add("X-Akahu-Id", _options.AppIdToken);

        // Akahu requires JSON body with all OAuth fields
        request.Content = JsonContent.Create(new
        {
            grant_type = "authorization_code",
            code,
            redirect_uri = _options.RedirectUri,
            client_id = _options.AppIdToken,
            client_secret = _options.AppSecret
        }, options: JsonOptions);

        _logger.LogDebug("Akahu token exchange: POST {Url}, redirect_uri={RedirectUri}, code_length={CodeLength}",
            fullUrl, _options.RedirectUri, code?.Length ?? 0);

        var response = await _httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response, "Token exchange");

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
        await EnsureSuccessAsync(response, "Get accounts");

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

        await EnsureSuccessAsync(response, "Get account");

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
            await EnsureSuccessAsync(response, "Get transactions");

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
            _logger.LogWarning("Reached maximum page limit ({MaxPages}) while fetching transactions",
                maxPages);
        }

        _logger.LogInformation("Fetched {TotalCount} transactions across {PageCount} pages",
            allTransactions.Count, pageCount);

        return allTransactions;
    }

    /// <summary>
    /// Get pending transactions for an account
    /// </summary>
    public async Task<IReadOnlyList<AkahuPendingTransaction>> GetPendingTransactionsAsync(
        string appIdToken,
        string userToken,
        string accountId,
        CancellationToken ct = default)
    {
        var url = $"accounts/{accountId}/transactions/pending";
        var request = CreateAuthenticatedRequest(HttpMethod.Get, url, appIdToken, userToken);
        var response = await _httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response, "Get pending transactions");

        var result = await response.Content.ReadFromJsonAsync<AkahuListResponse<AkahuPendingTransaction>>(JsonOptions, ct);
        return result?.Items ?? Array.Empty<AkahuPendingTransaction>();
    }

    /// <summary>
    /// Revoke user access token
    /// </summary>
    public async Task RevokeTokenAsync(string appIdToken, string accessToken, CancellationToken ct = default)
    {
        var baseUrl = _options.ApiBaseUrl.TrimEnd('/');
        var fullUrl = $"{baseUrl}/token";

        var request = new HttpRequestMessage(HttpMethod.Delete, fullUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("X-Akahu-Id", appIdToken);

        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var ex = new HttpRequestException(
                $"Akahu token revocation failed with status {response.StatusCode}",
                inner: null,
                response.StatusCode);
            _logger.LogError(ex, "Failed to revoke Akahu token: {StatusCode}", response.StatusCode);
            throw ex;
        }
    }

    /// <summary>
    /// Subscribe to an Akahu webhook type for the given user.
    /// </summary>
    public async Task SubscribeToWebhookAsync(string appIdToken, string userToken, string webhookType, string? state = null, CancellationToken ct = default)
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Post, "webhooks", appIdToken, userToken);
        var payload = new { webhook_type = webhookType, state };
        request.Content = JsonContent.Create(payload, options: JsonOptions);

        var response = await _httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response, $"Subscribe to webhook ({webhookType})");

        _logger.LogInformation("Subscribed to Akahu {WebhookType} webhook", webhookType);
    }

    /// <summary>
    /// Unsubscribe from an Akahu webhook.
    /// </summary>
    public async Task UnsubscribeFromWebhookAsync(string appIdToken, string userToken, string webhookId, CancellationToken ct = default)
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Delete, $"webhooks/{webhookId}", appIdToken, userToken);
        var response = await _httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response, "Unsubscribe from webhook");

        _logger.LogInformation("Unsubscribed from Akahu webhook");
    }

    /// <summary>
    /// List all webhook subscriptions for the user.
    /// </summary>
    public async Task<IReadOnlyList<AkahuWebhookSubscriptionInfo>> ListWebhooksAsync(string appIdToken, string userToken, CancellationToken ct = default)
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "webhooks", appIdToken, userToken);
        var response = await _httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response, "List webhooks");

        var result = await response.Content.ReadFromJsonAsync<AkahuListResponse<AkahuWebhookSubscriptionResponse>>(JsonOptions, ct);
        return (result?.Items ?? Array.Empty<AkahuWebhookSubscriptionResponse>())
            .Select(w => new AkahuWebhookSubscriptionInfo
            {
                Id = w.Id,
                WebhookType = w.WebhookType,
                State = w.State
            })
            .ToList();
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

    private Task EnsureSuccessAsync(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode)
            return Task.CompletedTask;

        // Extract request ID from response headers for correlation (safe to log)
        response.Headers.TryGetValues("X-Request-Id", out var requestIdValues);
        var requestId = requestIdValues?.FirstOrDefault();

        // Log only safe metadata — never log raw response bodies as they may contain tokens, PII, or account identifiers
        _logger.LogError(new HttpRequestException($"Akahu API error: {response.StatusCode}"),
            "Akahu API error - {Operation}: {StatusCode}, RequestId: {RequestId}",
            operation, response.StatusCode, requestId);

        throw response.StatusCode switch
        {
            HttpStatusCode.BadRequest => new AkahuApiException($"Akahu: {operation} - Bad request.", response.StatusCode),
            HttpStatusCode.Unauthorized => new UnauthorizedAccessException($"Akahu: {operation} - Unauthorized. Token may be expired or revoked."),
            HttpStatusCode.Forbidden => new AkahuApiException($"Akahu: {operation} - Forbidden. Insufficient permissions.", response.StatusCode),
            HttpStatusCode.NotFound => new AkahuApiException($"Akahu: {operation} - Resource not found.", response.StatusCode),
            HttpStatusCode.TooManyRequests => new AkahuApiException($"Akahu: {operation} - Rate limit exceeded. Please try again later.", response.StatusCode),
            _ => new AkahuApiException($"Akahu: {operation} failed with status {response.StatusCode}", response.StatusCode)
        };
    }
}

/// <summary>
/// Exception thrown when the Akahu API returns a non-success HTTP status code.
/// Distinguishes Akahu API errors (client/server errors from Akahu) from transport-level
/// failures (DNS, network, timeout) which remain plain <see cref="HttpRequestException"/>.
/// </summary>
public class AkahuApiException : HttpRequestException
{
    public HttpStatusCode AkahuStatusCode { get; }

    public AkahuApiException(string message, HttpStatusCode statusCode) : base(message)
    {
        AkahuStatusCode = statusCode;
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

public record AkahuPendingTransaction
{
    [JsonPropertyName("_account")]
    public string AccountId { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Type { get; init; } = string.Empty;
    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; init; }
}

public record AkahuWebhookSubscriptionResponse
{
    [JsonPropertyName("_id")]
    public string Id { get; init; } = string.Empty;
    [JsonPropertyName("webhook_type")]
    public string WebhookType { get; init; } = string.Empty;
    public string? State { get; init; }
}
