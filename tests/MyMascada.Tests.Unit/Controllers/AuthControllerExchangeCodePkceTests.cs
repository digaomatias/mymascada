using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Common.Security;
using MyMascada.WebAPI.Controllers;
using NSubstitute;
using System.Text.Json;

namespace MyMascada.Tests.Unit.Controllers;

/// <summary>
/// Tests for the PKCE binding on the Google OAuth /exchange-code endpoint and
/// the codeChallenge registration on /google-login-url. Uses a real
/// (ephemeral) data protector so protect/unprotect round-trips like prod.
/// </summary>
public class AuthControllerExchangeCodePkceTests
{
    private readonly IDataProtector _stateProtector;
    private readonly IDataProtector _authCodeProtector;
    private readonly AuthController _controller;

    public AuthControllerExchangeCodePkceTests()
    {
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        // Purposes must mirror AuthController: "OAuthState" for the state
        // blob, "OAuthAuthCode" for the short-lived auth-code blob.
        _stateProtector = dataProtectionProvider.CreateProtector("OAuthState");
        _authCodeProtector = dataProtectionProvider.CreateProtector("OAuthAuthCode");

        var appOptions = Options.Create(new AppOptions
        {
            FrontendUrl = "http://localhost:3000",
            ApiBaseUrl = "http://localhost:5126"
        });
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.EnvironmentName.Returns("Development");

        _controller = new AuthController(
            Substitute.For<IMediator>(),
            Substitute.For<IAuthenticationService>(),
            dataProtectionProvider,
            Substitute.For<IUserRepository>(),
            appOptions,
            environment,
            Substitute.For<IUserStatusService>(),
            Substitute.For<ISubscriptionService>(),
            Substitute.For<ILogger<AuthController>>());

        // GetGoogleLoginUrl resolves IConfiguration from RequestServices.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Google:ClientId"] = "test-client-id"
            })
            .Build();
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .BuildServiceProvider();

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services }
        };
    }

    private static string SerializeCodePayload(
        string? codeChallenge = null,
        string? codeChallengeMethod = null,
        DateTime? createdAt = null) =>
        JsonSerializer.Serialize(new
        {
            Token = "jwt-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            RefreshToken = (string?)null,
            RefreshTokenExpiresAt = (DateTime?)null,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            CodeChallenge = codeChallenge,
            CodeChallengeMethod = codeChallengeMethod
        });

    private string ProtectCode(
        string? codeChallenge = null,
        string? codeChallengeMethod = null,
        DateTime? createdAt = null) =>
        _authCodeProtector.Protect(SerializeCodePayload(codeChallenge, codeChallengeMethod, createdAt));

    private const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";

    // ── /exchange-code ──────────────────────────────────────────────────

    [Fact]
    public void ExchangeCode_WithoutChallenge_LegacyWebFlow_ReturnsOk()
    {
        var result = _controller.ExchangeCode(new ExchangeCodeRequest
        {
            Code = ProtectCode()
        });

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void ExchangeCode_S256Challenge_WithCorrectVerifier_ReturnsOk()
    {
        var challenge = PkceValidator.ComputeS256Challenge(Verifier);

        var result = _controller.ExchangeCode(new ExchangeCodeRequest
        {
            Code = ProtectCode(challenge, "S256"),
            CodeVerifier = Verifier
        });

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void ExchangeCode_PlainChallenge_IsRejectedEvenWithMatchingVerifier()
    {
        // Only S256 is supported — a code minted with method "plain" (which
        // no released client ever produced) must not be exchangeable.
        var secret = new string('s', 64);

        var result = _controller.ExchangeCode(new ExchangeCodeRequest
        {
            Code = ProtectCode(secret, "plain"),
            CodeVerifier = secret
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void ExchangeCode_Challenge_WithWrongVerifier_ReturnsBadRequest()
    {
        var challenge = PkceValidator.ComputeS256Challenge(Verifier);

        var result = _controller.ExchangeCode(new ExchangeCodeRequest
        {
            Code = ProtectCode(challenge, "S256"),
            CodeVerifier = new string('x', 43)
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void ExchangeCode_Challenge_WithMissingVerifier_ReturnsBadRequest()
    {
        var challenge = PkceValidator.ComputeS256Challenge(Verifier);

        var result = _controller.ExchangeCode(new ExchangeCodeRequest
        {
            Code = ProtectCode(challenge, "S256")
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void ExchangeCode_Challenge_WithTrailingNewlineVerifier_ReturnsBadRequest()
    {
        var challenge = PkceValidator.ComputeS256Challenge(Verifier);

        var result = _controller.ExchangeCode(new ExchangeCodeRequest
        {
            Code = ProtectCode(challenge, "S256"),
            CodeVerifier = Verifier + "\n"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void ExchangeCode_ExpiredCode_ReturnsBadRequest()
    {
        var result = _controller.ExchangeCode(new ExchangeCodeRequest
        {
            Code = ProtectCode(createdAt: DateTime.UtcNow.AddMinutes(-3))
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void ExchangeCode_TamperedCode_ReturnsBadRequest()
    {
        var result = _controller.ExchangeCode(new ExchangeCodeRequest
        {
            Code = "not-a-protected-payload"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void ExchangeCode_PayloadProtectedWithStatePurpose_ReturnsBadRequest()
    {
        // Cross-purpose isolation: a blob minted under the "OAuthState"
        // purpose — even one with a perfectly code-shaped payload — must not
        // be exchangeable. Unprotect with the "OAuthAuthCode" purpose fails
        // (purpose is mixed into the key derivation), closing the
        // cross-endpoint substitution class entirely.
        var codeShapedBlobUnderStatePurpose = _stateProtector.Protect(SerializeCodePayload());

        var result = _controller.ExchangeCode(new ExchangeCodeRequest
        {
            Code = codeShapedBlobUnderStatePurpose
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void ExchangeCode_OAuthStateBlob_ReturnsBadRequest()
    {
        // The original substitution scenario the reviewer flagged: feeding a
        // genuine state blob (as minted by google-login-url) into
        // exchange-code must fail at the cryptographic layer.
        var stateBlob = _stateProtector.Protect(JsonSerializer.Serialize(new
        {
            Nonce = Guid.NewGuid().ToString("N"),
            ReturnUrl = (string?)null,
            InviteCode = (string?)null,
            CodeChallenge = (string?)null,
            CodeChallengeMethod = (string?)null
        }));

        var result = _controller.ExchangeCode(new ExchangeCodeRequest
        {
            Code = stateBlob
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ── /google-login-url ───────────────────────────────────────────────

    [Fact]
    public void GetGoogleLoginUrl_WithValidChallenge_EmbedsChallengeInProtectedState()
    {
        var challenge = PkceValidator.ComputeS256Challenge(Verifier);

        var result = _controller.GetGoogleLoginUrl(
            returnUrl: "mymascada://auth/google-callback?n=abc",
            inviteCode: null,
            codeChallenge: challenge,
            codeChallengeMethod: "S256");

        var ok = Assert.IsType<OkObjectResult>(result);
        var redirectUrl = ok.Value!.GetType().GetProperty("RedirectUrl")!.GetValue(ok.Value) as string;
        Assert.NotNull(redirectUrl);

        var stateParam = Microsoft.AspNetCore.WebUtilities.QueryHelpers
            .ParseQuery(new Uri(redirectUrl!).Query)["state"].ToString();
        var statePayload = JsonSerializer.Deserialize<JsonElement>(_stateProtector.Unprotect(stateParam));

        Assert.Equal(challenge, statePayload.GetProperty("CodeChallenge").GetString());
        Assert.Equal("S256", statePayload.GetProperty("CodeChallengeMethod").GetString());
        Assert.Equal(
            "mymascada://auth/google-callback?n=abc",
            statePayload.GetProperty("ReturnUrl").GetString());
    }

    [Fact]
    public void GetGoogleLoginUrl_WithPlainMethod_ReturnsBadRequest()
    {
        // "plain" is rejected: the challenge travels in a loggable query
        // string, and with plain a logged challenge IS the verifier
        // (RFC 7636 §7.2).
        var result = _controller.GetGoogleLoginUrl(
            returnUrl: "mymascada://auth/google-callback",
            inviteCode: null,
            codeChallenge: new string('p', 64),
            codeChallengeMethod: "plain");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetGoogleLoginUrl_WithUnsupportedMethod_ReturnsBadRequest()
    {
        var result = _controller.GetGoogleLoginUrl(
            returnUrl: null,
            inviteCode: null,
            codeChallenge: new string('p', 64),
            codeChallengeMethod: "none");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetGoogleLoginUrl_WithMalformedChallenge_ReturnsBadRequest()
    {
        var result = _controller.GetGoogleLoginUrl(
            returnUrl: null,
            inviteCode: null,
            codeChallenge: "too short and has spaces",
            codeChallengeMethod: "S256");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetGoogleLoginUrl_WithoutChallenge_OmitsChallengeFromState()
    {
        var result = _controller.GetGoogleLoginUrl();

        var ok = Assert.IsType<OkObjectResult>(result);
        var redirectUrl = (string)ok.Value!.GetType().GetProperty("RedirectUrl")!.GetValue(ok.Value)!;
        var stateParam = Microsoft.AspNetCore.WebUtilities.QueryHelpers
            .ParseQuery(new Uri(redirectUrl).Query)["state"].ToString();
        var statePayload = JsonSerializer.Deserialize<JsonElement>(_stateProtector.Unprotect(stateParam));

        Assert.Equal(
            JsonValueKind.Null,
            statePayload.GetProperty("CodeChallenge").ValueKind);
    }
}
