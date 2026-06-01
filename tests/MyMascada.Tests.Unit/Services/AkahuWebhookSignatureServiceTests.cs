using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Infrastructure.Services.BankIntegration.Providers;

namespace MyMascada.Tests.Unit.Services;

public class AkahuWebhookSignatureServiceTests
{
    private const string KeyId = "key_test123";
    private const string Body = """{"webhook_type":"ACCOUNT","webhook_code":"MIGRATE"}""";

    [Fact]
    public async Task VerifySignatureAsync_WhenKeysEndpointReturnsBareStringItem_VerifiesValidSignature()
    {
        using var rsa = RSA.Create(2048);
        var signature = Convert.ToBase64String(rsa.SignData(
            Encoding.UTF8.GetBytes(Body), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

        // Akahu returns the PEM public key as a bare string in "item".
        var keysResponse = JsonSerializer.Serialize(new { success = true, item = rsa.ExportRSAPublicKeyPem() });
        var service = CreateService(keysResponse);

        var result = await service.VerifySignatureAsync(Body, signature, KeyId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task VerifySignatureAsync_WhenItemIsNotAString_ReturnsFalseWithoutThrowing()
    {
        using var rsa = RSA.Create(2048);
        var signature = Convert.ToBase64String(rsa.SignData(
            Encoding.UTF8.GetBytes(Body), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

        // Unexpected shape — "item" wrapped in an object. Must fail closed, not throw.
        var keysResponse = JsonSerializer.Serialize(new { success = true, item = new { key = rsa.ExportRSAPublicKeyPem() } });
        var service = CreateService(keysResponse);

        var result = await service.VerifySignatureAsync(Body, signature, KeyId);

        result.Should().BeFalse();
    }

    private static AkahuWebhookSignatureService CreateService(string keysResponseJson)
    {
        var handler = new DelegatingHandlerStub((request, _) =>
        {
            request.RequestUri!.AbsolutePath.Should().Contain("/keys/");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(keysResponseJson, Encoding.UTF8, "application/json")
            });
        });

        var options = Options.Create(new AkahuOptions
        {
            ApiBaseUrl = "https://api.akahu.io/v1/",
            WebhookSigningKeysCacheMinutes = 1440
        });

        return new AkahuWebhookSignatureService(
            new HttpClient(handler),
            new MemoryCache(new MemoryCacheOptions()),
            options,
            Substitute.For<IApplicationLogger<AkahuWebhookSignatureService>>());
    }

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
