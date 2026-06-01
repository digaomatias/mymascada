using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.Commands;
using MyMascada.Application.Features.BankConnections.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Tests.Unit.Commands;

public class InitiateAkahuConnectionCommandHandlerTests
{
    private static BankProviderModeResolution HostedOAuthMode() => new(
        "akahu",
        "hosted_oauth",
        new[]
        {
            new BankProviderAuthModeInfo
            {
                ModeId = "hosted_oauth",
                DisplayName = "MyMascada OAuth",
                RequiresUserCredentials = false
            }
        });

    private static BankProviderModeResolution PersonalTokensMode() => new(
        "akahu",
        "personal_tokens",
        new[]
        {
            new BankProviderAuthModeInfo
            {
                ModeId = "personal_tokens",
                DisplayName = "Personal tokens",
                RequiresUserCredentials = true
            }
        });

    [Fact]
    public async Task Handle_HostedOAuthMode_NoCredentials_ReturnsAuthorizationUrl()
    {
        var akahuApiClient = Substitute.For<IAkahuApiClient>();
        var credentialRepository = Substitute.For<IAkahuUserCredentialRepository>();
        var bankConnectionRepository = Substitute.For<IBankConnectionRepository>();
        var encryptionService = Substitute.For<ISettingsEncryptionService>();
        var modeResolver = Substitute.For<IBankProviderModeResolver>();
        var oauthStateStore = Substitute.For<IOAuthStateStore>();
        var logger = Substitute.For<IApplicationLogger<InitiateAkahuConnectionCommandHandler>>();

        modeResolver.Resolve("akahu").Returns(HostedOAuthMode());
        credentialRepository.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((AkahuUserCredential?)null);
        akahuApiClient.GetAuthorizationUrl(Arg.Any<string>(), "neo@example.com")
            .Returns("https://next.oauth.akahu.nz/?client_id=app_token_xxx");

        var handler = new InitiateAkahuConnectionCommandHandler(
            akahuApiClient, credentialRepository, bankConnectionRepository,
            encryptionService, modeResolver, oauthStateStore, logger);

        var userId = Guid.NewGuid();
        var result = await handler.Handle(new InitiateAkahuConnectionCommand(userId, "neo@example.com"), CancellationToken.None);

        result.IsPersonalAppMode.Should().BeFalse();
        result.RequiresCredentials.Should().BeFalse();
        result.AuthorizationUrl.Should().StartWith("https://next.oauth.akahu.nz/");
        result.State.Should().NotBeNullOrWhiteSpace();
        await oauthStateStore.Received(1).StoreAsync(userId, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExistingValidCredentials_SkipsOAuthAndReturnsAvailableAccounts()
    {
        // Regression: previously the hosted_oauth path always redirected to Akahu even when
        // the user already had valid credentials. Now we re-use them and return accounts
        // directly so the user can link additional accounts without re-authorizing.
        var akahuApiClient = Substitute.For<IAkahuApiClient>();
        var credentialRepository = Substitute.For<IAkahuUserCredentialRepository>();
        var bankConnectionRepository = Substitute.For<IBankConnectionRepository>();
        var encryptionService = Substitute.For<ISettingsEncryptionService>();
        var modeResolver = Substitute.For<IBankProviderModeResolver>();
        var oauthStateStore = Substitute.For<IOAuthStateStore>();
        var logger = Substitute.For<IApplicationLogger<InitiateAkahuConnectionCommandHandler>>();

        modeResolver.Resolve("akahu").Returns(HostedOAuthMode());

        var userId = Guid.NewGuid();
        var credential = new AkahuUserCredential
        {
            UserId = userId,
            EncryptedAppToken = "cipher-app",
            EncryptedUserToken = "cipher-user",
            ConsentRevokedAt = null
        };
        credentialRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(credential);
        encryptionService.DecryptSettings<string>("cipher-app").Returns("app_token_xxx");
        encryptionService.DecryptSettings<string>("cipher-user").Returns("user_token_yyy");

        akahuApiClient.GetAccountsWithCredentialsAsync("app_token_xxx", "user_token_yyy", Arg.Any<CancellationToken>())
            .Returns(new List<AkahuAccountInfo>
            {
                new() { Id = "acc_1", Name = "Everyday", FormattedAccount = "01-0001-0000000-00", Type = "CHECKING", BankName = "ANZ", Currency = "NZD", CurrentBalance = 100m }
            });

        bankConnectionRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<BankConnection>());

        var handler = new InitiateAkahuConnectionCommandHandler(
            akahuApiClient, credentialRepository, bankConnectionRepository,
            encryptionService, modeResolver, oauthStateStore, logger);

        var result = await handler.Handle(new InitiateAkahuConnectionCommand(userId), CancellationToken.None);

        result.IsPersonalAppMode.Should().BeTrue();
        result.RequiresCredentials.Should().BeFalse();
        result.AuthorizationUrl.Should().BeNull();
        result.AvailableAccounts.Should().NotBeNull();
        result.AvailableAccounts!.Should().HaveCount(1);
        result.AvailableAccounts!.First().Id.Should().Be("acc_1");

        // No OAuth state should have been persisted.
        await oauthStateStore.DidNotReceive().StoreAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        akahuApiClient.DidNotReceive().GetAuthorizationUrl(Arg.Any<string>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task Handle_ExistingCredentialsRejectedByAkahu_FallsBackToOAuthFlow()
    {
        // If the stored token is revoked upstream, hosted_oauth mode should silently
        // fall through to a fresh authorization URL rather than surfacing an error.
        var akahuApiClient = Substitute.For<IAkahuApiClient>();
        var credentialRepository = Substitute.For<IAkahuUserCredentialRepository>();
        var bankConnectionRepository = Substitute.For<IBankConnectionRepository>();
        var encryptionService = Substitute.For<ISettingsEncryptionService>();
        var modeResolver = Substitute.For<IBankProviderModeResolver>();
        var oauthStateStore = Substitute.For<IOAuthStateStore>();
        var logger = Substitute.For<IApplicationLogger<InitiateAkahuConnectionCommandHandler>>();

        modeResolver.Resolve("akahu").Returns(HostedOAuthMode());

        var userId = Guid.NewGuid();
        var credential = new AkahuUserCredential
        {
            UserId = userId,
            EncryptedAppToken = "cipher-app",
            EncryptedUserToken = "cipher-user",
            ConsentRevokedAt = null
        };
        credentialRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(credential);
        encryptionService.DecryptSettings<string>("cipher-app").Returns("app_token_xxx");
        encryptionService.DecryptSettings<string>("cipher-user").Returns("user_token_yyy");
        akahuApiClient.GetAccountsWithCredentialsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<AkahuAccountInfo>>(_ => throw new UnauthorizedAccessException("Token revoked"));
        akahuApiClient.GetAuthorizationUrl(Arg.Any<string>(), Arg.Any<string?>())
            .Returns("https://next.oauth.akahu.nz/?client_id=app_token_xxx");

        var handler = new InitiateAkahuConnectionCommandHandler(
            akahuApiClient, credentialRepository, bankConnectionRepository,
            encryptionService, modeResolver, oauthStateStore, logger);

        var result = await handler.Handle(new InitiateAkahuConnectionCommand(userId), CancellationToken.None);

        result.AuthorizationUrl.Should().StartWith("https://next.oauth.akahu.nz/");
        result.RequiresCredentials.Should().BeFalse();
        await oauthStateStore.Received(1).StoreAsync(userId, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RevokedCredentials_HostedOAuthMode_ReturnsAuthorizationUrl()
    {
        // ConsentRevokedAt != null means the user explicitly disconnected; we must not
        // try to reuse the row — issue a fresh authorization URL.
        var akahuApiClient = Substitute.For<IAkahuApiClient>();
        var credentialRepository = Substitute.For<IAkahuUserCredentialRepository>();
        var bankConnectionRepository = Substitute.For<IBankConnectionRepository>();
        var encryptionService = Substitute.For<ISettingsEncryptionService>();
        var modeResolver = Substitute.For<IBankProviderModeResolver>();
        var oauthStateStore = Substitute.For<IOAuthStateStore>();
        var logger = Substitute.For<IApplicationLogger<InitiateAkahuConnectionCommandHandler>>();

        modeResolver.Resolve("akahu").Returns(HostedOAuthMode());

        var userId = Guid.NewGuid();
        credentialRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new AkahuUserCredential
            {
                UserId = userId,
                EncryptedAppToken = "cipher-app",
                EncryptedUserToken = "cipher-user",
                ConsentRevokedAt = DateTime.UtcNow.AddDays(-1)
            });
        akahuApiClient.GetAuthorizationUrl(Arg.Any<string>(), Arg.Any<string?>())
            .Returns("https://next.oauth.akahu.nz/?client_id=app_token_xxx");

        var handler = new InitiateAkahuConnectionCommandHandler(
            akahuApiClient, credentialRepository, bankConnectionRepository,
            encryptionService, modeResolver, oauthStateStore, logger);

        var result = await handler.Handle(new InitiateAkahuConnectionCommand(userId), CancellationToken.None);

        result.AuthorizationUrl.Should().StartWith("https://next.oauth.akahu.nz/");
        await akahuApiClient.DidNotReceive().GetAccountsWithCredentialsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PersonalMode_NoCredentials_ReturnsRequiresCredentials()
    {
        var akahuApiClient = Substitute.For<IAkahuApiClient>();
        var credentialRepository = Substitute.For<IAkahuUserCredentialRepository>();
        var bankConnectionRepository = Substitute.For<IBankConnectionRepository>();
        var encryptionService = Substitute.For<ISettingsEncryptionService>();
        var modeResolver = Substitute.For<IBankProviderModeResolver>();
        var oauthStateStore = Substitute.For<IOAuthStateStore>();
        var logger = Substitute.For<IApplicationLogger<InitiateAkahuConnectionCommandHandler>>();

        modeResolver.Resolve("akahu").Returns(new BankProviderModeResolution(
            "akahu",
            "personal_tokens",
            new[]
            {
                new BankProviderAuthModeInfo
                {
                    ModeId = "personal_tokens",
                    DisplayName = "Personal tokens",
                    RequiresUserCredentials = true
                }
            }));

        credentialRepository.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((MyMascada.Domain.Entities.AkahuUserCredential?)null);

        var handler = new InitiateAkahuConnectionCommandHandler(
            akahuApiClient,
            credentialRepository,
            bankConnectionRepository,
            encryptionService,
            modeResolver,
            oauthStateStore,
            logger);

        var result = await handler.Handle(new InitiateAkahuConnectionCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsPersonalAppMode.Should().BeTrue();
        result.RequiresCredentials.Should().BeTrue();
        result.AuthorizationUrl.Should().BeNull();
    }
}
