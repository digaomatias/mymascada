using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Infrastructure.Services.BankIntegration.Providers;

/// <summary>
/// Verifies Akahu webhook signatures by fetching RSA public keys from Akahu's API.
/// Keys are cached for the configured duration (default 24 hours).
/// </summary>
public partial class AkahuWebhookSignatureService : IAkahuWebhookSignatureService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly AkahuOptions _options;
    private readonly IApplicationLogger<AkahuWebhookSignatureService> _logger;

    private const string CacheKeyPrefix = "akahu_signing_key_";
    private const int MaxKeyIdLength = 64;

    [GeneratedRegex(@"^[a-zA-Z0-9_\-]+$")]
    private static partial Regex KeyIdFormatRegex();

    public AkahuWebhookSignatureService(
        HttpClient httpClient,
        IMemoryCache cache,
        IOptions<AkahuOptions> options,
        IApplicationLogger<AkahuWebhookSignatureService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> VerifySignatureAsync(string body, string signature, string keyId, CancellationToken ct = default)
    {
        if (!IsValidKeyId(keyId))
        {
            _logger.LogWarning("Invalid keyId format rejected");
            return false;
        }

        try
        {
            var publicKey = await GetSigningKeyAsync(keyId, ct);
            if (publicKey == null)
            {
                _logger.LogWarning("Failed to retrieve signing key {KeyId} from Akahu", keyId);
                return false;
            }

            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKey);

            return rsa.VerifyData(
                Encoding.UTF8.GetBytes(body),
                Convert.FromBase64String(signature),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying Akahu webhook signature for key {KeyId}", keyId);
            return false;
        }
    }

    private async Task<string?> GetSigningKeyAsync(string keyId, CancellationToken ct)
    {
        var cacheKey = CacheKeyPrefix + keyId;

        if (_cache.TryGetValue(cacheKey, out string? cachedKey))
            return cachedKey;

        try
        {
            var baseUrl = _options.ApiBaseUrl.TrimEnd('/');
            var escapedKeyId = Uri.EscapeDataString(keyId);
            var response = await _httpClient.GetAsync($"{baseUrl}/keys/{escapedKeyId}", ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch signing key {KeyId}: {StatusCode}", keyId, response.StatusCode);
                CacheNullResult(cacheKey);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("success", out var success) || !success.GetBoolean())
            {
                _logger.LogWarning("Akahu keys endpoint returned success=false for key {KeyId}", keyId);
                CacheNullResult(cacheKey);
                return null;
            }

            var publicKey = doc.RootElement.GetProperty("item").GetProperty("key").GetString();
            if (string.IsNullOrEmpty(publicKey))
            {
                _logger.LogWarning("Signing key {KeyId} had empty key value", keyId);
                CacheNullResult(cacheKey);
                return null;
            }

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.WebhookSigningKeysCacheMinutes)
            };
            _cache.Set(cacheKey, publicKey, cacheOptions);

            _logger.LogInformation("Cached Akahu signing key {KeyId} for {Minutes} minutes", keyId, _options.WebhookSigningKeysCacheMinutes);
            return publicKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Akahu signing key {KeyId}", keyId);
            CacheNullResult(cacheKey);
            return null;
        }
    }

    private static bool IsValidKeyId(string? keyId)
    {
        return !string.IsNullOrEmpty(keyId)
            && keyId.Length <= MaxKeyIdLength
            && KeyIdFormatRegex().IsMatch(keyId);
    }

    private void CacheNullResult(string cacheKey)
    {
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        };
        _cache.Set<string?>(cacheKey, null, cacheOptions);
    }
}
