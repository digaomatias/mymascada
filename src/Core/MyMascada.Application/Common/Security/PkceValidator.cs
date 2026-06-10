using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MyMascada.Application.Common.Security;

/// <summary>
/// PKCE (RFC 7636) helpers used to bind the Google OAuth exchange-code flow
/// to the client that initiated it. Mobile clients send a code challenge when
/// requesting the login URL and must present the matching code verifier when
/// exchanging the short-lived protected auth code. Web clients that don't
/// send a challenge are unaffected (PKCE is enforced only when a challenge
/// was registered at flow initiation).
/// </summary>
public static partial class PkceValidator
{
    public const string MethodS256 = "S256";
    public const string MethodPlain = "plain";

    [GeneratedRegex("^[A-Za-z0-9\\-._~]{43,128}$")]
    private static partial Regex UnreservedPattern();

    /// <summary>
    /// Returns true when the method is a supported PKCE transform
    /// ("S256" or "plain", case-sensitive per RFC 7636).
    /// </summary>
    public static bool IsSupportedMethod(string method) =>
        method is MethodS256 or MethodPlain;

    /// <summary>
    /// Validates the code challenge format: 43–128 characters from the
    /// RFC 3986 unreserved set (also covers base64url-encoded S256 output).
    /// </summary>
    public static bool IsValidChallengeFormat(string challenge) =>
        !string.IsNullOrEmpty(challenge) && UnreservedPattern().IsMatch(challenge);

    /// <summary>
    /// Verifies a code verifier against the challenge registered at flow
    /// initiation. Uses fixed-time comparison to avoid timing side channels.
    /// </summary>
    public static bool Verify(string codeChallenge, string codeChallengeMethod, string? codeVerifier)
    {
        if (string.IsNullOrEmpty(codeChallenge) || string.IsNullOrEmpty(codeVerifier))
        {
            return false;
        }

        if (!UnreservedPattern().IsMatch(codeVerifier))
        {
            return false;
        }

        var computedChallenge = codeChallengeMethod switch
        {
            MethodS256 => ComputeS256Challenge(codeVerifier),
            MethodPlain => codeVerifier,
            _ => null
        };

        if (computedChallenge is null)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(computedChallenge),
            Encoding.ASCII.GetBytes(codeChallenge));
    }

    /// <summary>
    /// Computes the S256 code challenge for a verifier:
    /// BASE64URL(SHA256(ASCII(verifier))) without padding.
    /// </summary>
    public static string ComputeS256Challenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
