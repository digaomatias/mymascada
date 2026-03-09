namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Verifies Akahu webhook request signatures using RSA public keys.
/// </summary>
public interface IAkahuWebhookSignatureService
{
    /// <summary>
    /// Verifies the signature of an incoming Akahu webhook request.
    /// </summary>
    /// <param name="body">The raw request body string</param>
    /// <param name="signature">The value of the X-Akahu-Signature header</param>
    /// <param name="keyId">The value of the X-Akahu-Signing-Key header</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if the signature is valid</returns>
    Task<bool> VerifySignatureAsync(string body, string signature, string keyId, CancellationToken ct = default);
}
