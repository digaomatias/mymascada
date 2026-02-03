using Microsoft.AspNetCore.DataProtection;
using System.Text.Json;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Infrastructure.Services.Security;

/// <summary>
/// Service for encrypting/decrypting sensitive provider settings using ASP.NET Core Data Protection API.
/// Used to protect OAuth tokens, API keys, and other sensitive configuration stored in BankConnection.EncryptedSettings.
/// </summary>
public class SettingsEncryptionService : ISettingsEncryptionService
{
    private readonly IDataProtector _protector;
    private readonly IApplicationLogger<SettingsEncryptionService> _logger;
    private const string Purpose = "BankConnection.Settings.v1";

    public SettingsEncryptionService(
        IDataProtectionProvider provider,
        IApplicationLogger<SettingsEncryptionService> logger)
    {
        _protector = provider.CreateProtector(Purpose);
        _logger = logger;
    }

    /// <inheritdoc />
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        try
        {
            return _protector.Protect(plainText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt data");
            throw new InvalidOperationException("Encryption failed", ex);
        }
    }

    /// <inheritdoc />
    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return string.Empty;

        try
        {
            return _protector.Unprotect(cipherText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt data - data may be corrupted or key may have changed");
            throw new InvalidOperationException("Decryption failed - settings may be corrupted", ex);
        }
    }

    /// <inheritdoc />
    public T? DecryptSettings<T>(string? encryptedSettings) where T : class
    {
        if (string.IsNullOrEmpty(encryptedSettings))
            return null;

        try
        {
            var json = Decrypt(encryptedSettings);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize decrypted settings to type {Type}", typeof(T).Name);
            throw new InvalidOperationException($"Failed to deserialize settings to {typeof(T).Name}", ex);
        }
    }

    /// <inheritdoc />
    public string EncryptSettings<T>(T settings) where T : class
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            var json = JsonSerializer.Serialize(settings);
            return Encrypt(json);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to serialize settings of type {Type}", typeof(T).Name);
            throw new InvalidOperationException($"Failed to serialize settings of {typeof(T).Name}", ex);
        }
    }
}
