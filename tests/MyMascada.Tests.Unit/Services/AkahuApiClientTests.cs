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
        await client.RevokeTokenAsync(TestAppToken, "user_token_to_revoke");

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.ToString().Should().Be("https://api.akahu.io/v1/token");
        capturedRequest.Method.Should().Be(HttpMethod.Delete);

        // Assert - Bearer token for the user token being revoked
        capturedRequest.Headers.Authorization.Should().NotBeNull();
        capturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        capturedRequest.Headers.Authorization.Parameter.Should().Be("user_token_to_revoke");

        // Assert - X-Akahu-Id header uses the explicitly passed appIdToken, not config
        capturedRequest.Headers.GetValues("X-Akahu-Id").Should().ContainSingle()
            .Which.Should().Be(TestAppToken);
    }

    [Fact]
    public async Task GetAccountsInternalAsync_Status401_ThrowsUnauthorizedAccessException()
    {
        var handler = new DelegatingHandlerStub((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var client = CreateClient(handler);

        Func<Task> act = async () =>
            await client.GetAccountsInternalAsync(TestAppToken, "user_token_abc");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetAccountsInternalAsync_Non401Errors_ThrowAkahuApiExceptionWithStatusCode(HttpStatusCode statusCode)
    {
        var handler = new DelegatingHandlerStub((_, _) =>
            Task.FromResult(new HttpResponseMessage(statusCode)));
        var client = CreateClient(handler);

        Func<Task> act = async () =>
            await client.GetAccountsInternalAsync(TestAppToken, "user_token_abc");

        var ex = await act.Should().ThrowAsync<AkahuApiException>();
        ex.Which.AkahuStatusCode.Should().Be(statusCode);
    }

    [Fact]
    public async Task GetAccountInternalAsync_ErrorLog_ExcludesSensitiveIdentifiers()
    {
        const string sensitiveAccountId = "acc_sensitive_123";
        const string sensitiveTokenFragment = "user_token_sensitive_456";

        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            RequestMessage = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.akahu.io/v1/accounts/{sensitiveAccountId}"),
            Content = new StringContent(
                $"{{\"error\":\"invalid token {sensitiveTokenFragment}\"}}",
                Encoding.UTF8,
                "application/json")
        };
        response.Headers.Add("X-Request-Id", "req_abc123");

        var handler = new DelegatingHandlerStub((_, _) => Task.FromResult(response));
        var (client, logger) = CreateClientWithLogger(handler);

        Func<Task> act = async () =>
            await client.GetAccountInternalAsync(TestAppToken, "user_token_abc", sensitiveAccountId);

        await act.Should().ThrowAsync<AkahuApiException>();

        var logCall = logger.ReceivedCalls().Single();
        var callArguments = logCall.GetArguments();

        callArguments[1].Should().Be("Akahu API error - {Operation}: {StatusCode}, RequestId: {RequestId}");

        var structuredArgs = callArguments[2].Should().BeAssignableTo<object[]>().Subject;
        structuredArgs.Should().HaveCount(3);
        structuredArgs[0].Should().Be("Get account");
        structuredArgs[1].Should().Be(HttpStatusCode.BadRequest);
        structuredArgs[2].Should().Be("req_abc123");

        structuredArgs
            .Select(arg => arg?.ToString())
            .Should()
            .NotContain(value =>
                !string.IsNullOrEmpty(value) &&
                (value.Contains(sensitiveAccountId, StringComparison.Ordinal) ||
                 value.Contains(sensitiveTokenFragment, StringComparison.Ordinal)));
    }

    private static AkahuApiClient CreateClient(DelegatingHandlerStub handler)
    {
        return CreateClientWithLogger(handler).Client;
    }

    private static (AkahuApiClient Client, IApplicationLogger<AkahuApiClient> Logger) CreateClientWithLogger(DelegatingHandlerStub handler)
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
        return (new AkahuApiClient(httpClient, options, logger), logger);
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
