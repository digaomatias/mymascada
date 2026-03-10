using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Infrastructure.Services.BankIntegration.Providers;

namespace MyMascada.Tests.Unit.Services;

public class AkahuApiClientTests
{
    private const string TestAppToken = "app_token_123";
    private const string TestAppSecret = "secret_456";
    private const string TestRedirectUri = "http://localhost:3000/settings/bank-connections/callback";
    private const string TestApiBaseUrl = "https://api.akahu.io/v1/";

    [Fact]
    public void GetAuthorizationUrl_UsesRootOAuthUrlAndDeduplicatesScopes()
    {
        var options = Options.Create(new AkahuOptions
        {
            AppIdToken = TestAppToken,
            RedirectUri = TestRedirectUri,
            OAuthBaseUrl = "https://next.oauth.akahu.nz",
            DefaultScopes = new[] { "ENDURING_CONSENT", "ENDURING_CONSENT" }
        });

        var logger = Substitute.For<IApplicationLogger<AkahuApiClient>>();
        var client = new AkahuApiClient(new HttpClient(), options, logger);

        var result = client.GetAuthorizationUrl("state_123", "rod@example.com");

        result.Should().StartWith("https://next.oauth.akahu.nz/?");
        result.Should().NotContain("/authorize");
        result.Should().Contain("scope=ENDURING_CONSENT");
        result.Should().NotContain("ENDURING_CONSENT%20ENDURING_CONSENT");
        result.Should().Contain("email=rod%40example.com");
    }

    [Fact]
    public async Task ExchangeCodeForToken_SendsJsonWithBasicAuthAndAkahuIdHeader()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        byte[]? capturedBody = null;

        var handler = new DelegatingHandlerStub(async (request, ct) =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsByteArrayAsync(ct);

            var responseJson = JsonSerializer.Serialize(new
            {
                success = true,
                access_token = "user_token_abc",
                token_type = "bearer",
                scope = "ENDURING_CONSENT"
            });

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler);

        // Act
        var result = await client.ExchangeCodeForTokenInternalAsync("auth_code_xyz");

        // Assert - correct URL
        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.ToString().Should().Be("https://api.akahu.io/v1/token");
        capturedRequest.Method.Should().Be(HttpMethod.Post);

        // Assert - JSON content type
        capturedRequest.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");

        // Assert - Basic Auth header with base64(app_token:app_secret)
        capturedRequest.Headers.Authorization.Should().NotBeNull();
        capturedRequest.Headers.Authorization!.Scheme.Should().Be("Basic");
        var expectedCredentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{TestAppToken}:{TestAppSecret}"));
        capturedRequest.Headers.Authorization.Parameter.Should().Be(expectedCredentials);

        // Assert - X-Akahu-Id header
        capturedRequest.Headers.GetValues("X-Akahu-Id").Should().ContainSingle()
            .Which.Should().Be(TestAppToken);

        // Assert - JSON body contains required fields
        var bodyJson = JsonDocument.Parse(capturedBody);
        bodyJson.RootElement.GetProperty("grant_type").GetString().Should().Be("authorization_code");
        bodyJson.RootElement.GetProperty("code").GetString().Should().Be("auth_code_xyz");
        bodyJson.RootElement.GetProperty("redirect_uri").GetString().Should().Be(TestRedirectUri);
        bodyJson.RootElement.GetProperty("client_id").GetString().Should().Be(TestAppToken);
        bodyJson.RootElement.GetProperty("client_secret").GetString().Should().Be(TestAppSecret);

        // Assert - response parsed correctly
        result.AccessToken.Should().Be("user_token_abc");
        result.TokenType.Should().Be("bearer");
    }

    [Fact]
    public async Task RevokeToken_SendsDeleteWithAkahuIdHeader()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;

        var handler = new DelegatingHandlerStub((request, _) =>
        {
            capturedRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var client = CreateClient(handler);

        // Act
        await client.RevokeTokenAsync("user_token_to_revoke");

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.ToString().Should().Be("https://api.akahu.io/v1/token");
        capturedRequest.Method.Should().Be(HttpMethod.Delete);

        // Assert - Bearer token for the user token being revoked
        capturedRequest.Headers.Authorization.Should().NotBeNull();
        capturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        capturedRequest.Headers.Authorization.Parameter.Should().Be("user_token_to_revoke");

        // Assert - X-Akahu-Id header present
        capturedRequest.Headers.GetValues("X-Akahu-Id").Should().ContainSingle()
            .Which.Should().Be(TestAppToken);
    }

    private static AkahuApiClient CreateClient(DelegatingHandlerStub handler)
    {
        var options = Options.Create(new AkahuOptions
        {
            AppIdToken = TestAppToken,
            AppSecret = TestAppSecret,
            RedirectUri = TestRedirectUri,
            ApiBaseUrl = TestApiBaseUrl,
            OAuthBaseUrl = "https://oauth.akahu.nz"
        });

        var logger = Substitute.For<IApplicationLogger<AkahuApiClient>>();
        var httpClient = new HttpClient(handler);
        return new AkahuApiClient(httpClient, options, logger);
    }

    /// <summary>
    /// Test helper to intercept HTTP requests without making real network calls.
    /// </summary>
    private class DelegatingHandlerStub : DelegatingHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public DelegatingHandlerStub(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }
}
