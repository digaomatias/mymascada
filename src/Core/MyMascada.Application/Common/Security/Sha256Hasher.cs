using System.Security.Cryptography;
using System.Text;

namespace MyMascada.Application.Common.Security;

/// <summary>
/// Shared SHA-256 hashing utility for tokens and secrets that need
/// one-way hashing before storage (e.g., refresh tokens, webhook secrets).
/// </summary>
public static class Sha256Hasher
{
    /// <summary>
    /// Computes the SHA-256 hash of the input string and returns it as a lowercase hex string.
    /// </summary>
    public static string Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
