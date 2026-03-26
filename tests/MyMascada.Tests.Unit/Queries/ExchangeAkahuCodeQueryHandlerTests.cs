using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.Queries;
using MyMascada.Domain.Entities;

namespace MyMascada.Tests.Unit.Queries;

public class ExchangeAkahuCodeQueryHandlerTests
{
    [Fact]
    public async Task Handle_PersistsOAuthCredential_WhenStateIsMissing()
    {
        var userId = Guid.NewGuid();
        var akahuApiClient = Substitute.For<IAkahuApiClient>();
        var credentialRepository = Substitute.For<IAkahuUserCredentialRepository>();
        var bankConnectionRepository = Substitute.For<IBankConnectionRepository>();
        var encryptionService = Substitute.For<ISettingsEncryptionService>();
        var logger = Substitute.For<IApplicationLogger<ExchangeAkahuCodeQueryHandler>>();

        akahuApiClient.ExchangeCodeForTokenAsync("code-123", Arg.Any<CancellationToken>())
            .Returns(new AkahuTokenResponse
            {
                AccessToken = "user_token_oauth",
                TokenType = "Bearer",
                Scope = "ENDURING_CONSENT"
            });

        akahuApiClient.GetAccountsWithCredentialsAsync("app_token_123", "user_token_oauth", Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new AkahuAccountInfo
                {
                    Id = "acc_123",
                    Name = "Everyday",
                    FormattedAccount = "12-1234-1234567-00",
                    Type = "CHECKING",
                    BankName = "ANZ",
                    Currency = "NZD",
                    CurrentBalance = 123.45m
                }
            });

        credentialRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((AkahuUserCredential?)null);

        bankConnectionRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<BankConnection>());

        encryptionService.EncryptSettings("app_token_123").Returns("enc_app");
        encryptionService.EncryptSettings("user_token_oauth").Returns("enc_user");

        var handler = new ExchangeAkahuCodeQueryHandler(
            akahuApiClient,
            credentialRepository,
            bankConnectionRepository,
            encryptionService,
            logger);

        var result = await handler.Handle(
            new ExchangeAkahuCodeQuery(userId, "code-123", null, "app_token_123"),
            CancellationToken.None);

        result.Accounts.Should().ContainSingle(a => a.Id == "acc_123" && !a.IsAlreadyLinked);

        await credentialRepository.Received(1).AddAsync(
            Arg.Is<AkahuUserCredential>(c =>
                c.UserId == userId &&
                c.EncryptedAppToken == "enc_app" &&
                c.EncryptedUserToken == "enc_user" &&
                c.LastValidatedAt.HasValue),
            Arg.Any<CancellationToken>());
    }
}
