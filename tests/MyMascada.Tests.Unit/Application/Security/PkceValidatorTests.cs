using System.Security.Cryptography;
using System.Text;
using MyMascada.Application.Common.Security;

namespace MyMascada.Tests.Unit.Application.Security;

/// <summary>
/// Tests for the PKCE (RFC 7636) helpers used by the Google OAuth
/// exchange-code flow to bind code exchange to the initiating client.
/// </summary>
public class PkceValidatorTests
{
    // RFC 7636 Appendix B reference vector
    private const string RfcVerifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
    private const string RfcS256Challenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

    [Fact]
    public void IsSupportedMethod_AcceptsS256()
    {
        Assert.True(PkceValidator.IsSupportedMethod("S256"));
    }

    [Theory]
    [InlineData("plain")] // deliberately rejected — see RFC 7636 §7.2
    [InlineData("s256")] // case-sensitive per RFC 7636
    [InlineData("PLAIN")]
    [InlineData("none")]
    [InlineData("")]
    public void IsSupportedMethod_RejectsEverythingElse(string method)
    {
        Assert.False(PkceValidator.IsSupportedMethod(method));
    }

    [Fact]
    public void IsValidChallengeFormat_AcceptsUnreservedChars43To128()
    {
        Assert.True(PkceValidator.IsValidChallengeFormat(RfcS256Challenge));
        Assert.True(PkceValidator.IsValidChallengeFormat(new string('a', 43)));
        Assert.True(PkceValidator.IsValidChallengeFormat(new string('a', 128)));
        Assert.True(PkceValidator.IsValidChallengeFormat("Az09-._~" + new string('x', 40)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("too-short")]
    [InlineData("contains spaces contains spaces contains spaces contains")]
    [InlineData("has+plus/and=padding-chars-which-are-not-unreserved-12345678")]
    public void IsValidChallengeFormat_RejectsInvalidValues(string challenge)
    {
        Assert.False(PkceValidator.IsValidChallengeFormat(challenge));
    }

    [Fact]
    public void IsValidChallengeFormat_RejectsOverlongValue()
    {
        Assert.False(PkceValidator.IsValidChallengeFormat(new string('a', 129)));
    }

    [Fact]
    public void IsValidChallengeFormat_RejectsTrailingNewline()
    {
        // Regression: with the `$` anchor, .NET regexes match just before a
        // trailing \n, so "<valid value>\n" used to pass. The pattern must
        // anchor with \z.
        Assert.False(PkceValidator.IsValidChallengeFormat(RfcS256Challenge + "\n"));
        Assert.False(PkceValidator.IsValidChallengeFormat(new string('a', 43) + "\n"));
    }

    [Fact]
    public void ComputeS256Challenge_MatchesRfc7636ReferenceVector()
    {
        Assert.Equal(RfcS256Challenge, PkceValidator.ComputeS256Challenge(RfcVerifier));
    }

    [Fact]
    public void Verify_S256_AcceptsMatchingVerifier()
    {
        Assert.True(PkceValidator.Verify(RfcS256Challenge, "S256", RfcVerifier));
    }

    [Fact]
    public void Verify_S256_RejectsWrongVerifier()
    {
        var wrongVerifier = new string('b', 43);
        Assert.False(PkceValidator.Verify(RfcS256Challenge, "S256", wrongVerifier));
    }

    [Fact]
    public void Verify_RejectsPlainMethod()
    {
        // Even a "matching" plain pair must be rejected — only S256 is
        // supported (the challenge travels in loggable query strings, and
        // with plain a logged challenge IS the verifier).
        var secret = new string('k', 64);
        Assert.False(PkceValidator.Verify(secret, "plain", secret));
    }

    [Fact]
    public void Verify_RejectsMissingVerifier()
    {
        Assert.False(PkceValidator.Verify(RfcS256Challenge, "S256", null));
        Assert.False(PkceValidator.Verify(RfcS256Challenge, "S256", ""));
    }

    [Fact]
    public void Verify_RejectsUnknownMethod()
    {
        Assert.False(PkceValidator.Verify(RfcS256Challenge, "none", RfcVerifier));
    }

    [Fact]
    public void Verify_RejectsVerifierWithInvalidFormat()
    {
        Assert.False(PkceValidator.Verify(RfcS256Challenge, "S256", "short"));
    }

    [Fact]
    public void Verify_RejectsVerifierWithTrailingNewline()
    {
        Assert.False(PkceValidator.Verify(RfcS256Challenge, "S256", RfcVerifier + "\n"));
    }

    [Fact]
    public void ComputeS256Challenge_IsBase64UrlWithoutPadding()
    {
        var verifier = new string('v', 50);
        var challenge = PkceValidator.ComputeS256Challenge(verifier);

        Assert.DoesNotContain('+', challenge);
        Assert.DoesNotContain('/', challenge);
        Assert.DoesNotContain('=', challenge);

        // Round-trip: decoding the base64url value yields the SHA-256 hash.
        var padded = challenge.Replace('-', '+').Replace('_', '/') + "=";
        var decoded = Convert.FromBase64String(padded);
        var expected = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        Assert.Equal(expected, decoded);
    }
}
