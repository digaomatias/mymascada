namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Service for encrypting and decrypting sensitive settings.
/// Used to protect provider-specific configuration (OAuth tokens, API keys, etc.)
/// stored in BankConnection.EncryptedSettings.
/// </summary>
public interface ISettingsEncryptionService
{
    /// <summary>
    /// Encrypts a plain text string using AES-256 encryption.
    /// </summary>
    /// <param name="plainText">The plain text to encrypt</param>
    /// <returns>The encrypted cipher text (base64 encoded)</returns>
    string Encrypt(string plainText);

    /// <summary>
    /// Decrypts a cipher text string that was encrypted with this service.
    /// </summary>
    /// <param name="cipherText">The encrypted cipher text (base64 encoded)</param>
    /// <returns>The decrypted plain text</returns>
    string Decrypt(string cipherText);

    /// <summary>
    /// Decrypts and deserializes encrypted JSON settings to a strongly-typed object.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to</typeparam>
    /// <param name="encryptedSettings">The encrypted settings JSON, or null</param>
    /// <returns>The deserialized settings object, or null if input is null/empty</returns>
    T? DecryptSettings<T>(string? encryptedSettings) where T : class;

    /// <summary>
    /// Serializes and encrypts a settings object to an encrypted JSON string.
    /// </summary>
    /// <typeparam name="T">The type of settings object</typeparam>
    /// <param name="settings">The settings object to encrypt</param>
    /// <returns>The encrypted settings JSON</returns>
    string EncryptSettings<T>(T settings) where T : class;
}
