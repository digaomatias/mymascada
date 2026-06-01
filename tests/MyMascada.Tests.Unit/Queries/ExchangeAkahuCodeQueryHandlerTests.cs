using MyMascada.Application.BackgroundJobs;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.Queries;
using MyMascada.Domain.Entities;

namespace MyMascada.Tests.Unit.Queries;

public class ExchangeAkahuCodeQueryHandlerTests
{
    [Fact]
    public async Task Handle_ValidState_PersistsOAuthCredentialAndReturnsAccounts()
    {
        var userId = Guid.NewGuid();
        var akahuApiClient = Substitute.For<IAkahuApiClient>();
        var credentialRepository = Substitute.For<IAkahuUserCredentialRepository>();
        var bankConnectionRepository = Substitute.For<IBankConnectionRepository>();
        var encryptionService = Substitute.For<ISettingsEncryptionService>();
        var oauthStateStore = Substitute.For<IOAuthStateStore>();
        var webhookSubscriptionService = Substitute.For<IAkahuWebhookSubscriptionService>();
        var migrationJobService = Substitute.For<IAkahuMigrationJobService>();
        var logger = Substitute.For<IApplicationLogger<ExchangeAkahuCodeQueryHandler>>();

        webhookSubscriptionService.EnsureSubscriptionsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new EnsureSubscriptionsResult(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), new Dictionary<string, string>()));

        oauthStateStore.ValidateAndConsumeAsync(userId, "valid-state", Arg.Any<CancellationToken>())
            .Returns(true);

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
            oauthStateStore,
            webhookSubscriptionService,
            migrationJobService,
            logger);

        var result = await handler.Handle(
            new ExchangeAkahuCodeQuery(userId, "code-123", "valid-state", "app_token_123"),
            CancellationToken.None);

        result.Accounts.Should().ContainSingle(a => a.Id == "acc_123" && !a.IsAlreadyLinked);

        await credentialRepository.Received(1).AddAsync(
            Arg.Is<AkahuUserCredential>(c =>
                c.UserId == userId &&
                c.EncryptedAppToken == "enc_app" &&
                c.EncryptedUserToken == "enc_user" &&
                c.LastValidatedAt.HasValue &&
                c.ConsentScope == "ENDURING_CONSENT" &&
                c.ConsentGrantedAt.HasValue &&
                c.ConsentCorrelationId == "valid-state"),
            Arg.Any<CancellationToken>());

        await webhookSubscriptionService.Received(1).EnsureSubscriptionsAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PersistsConsentCorrelationId_WhenStateIsProvided()
    {
        var userId = Guid.NewGuid();
        var akahuApiClient = Substitute.For<IAkahuApiClient>();
        var credentialRepository = Substitute.For<IAkahuUserCredentialRepository>();
        var bankConnectionRepository = Substitute.For<IBankConnectionRepository>();
        var encryptionService = Substitute.For<ISettingsEncryptionService>();
        var oauthStateStore = Substitute.For<IOAuthStateStore>();
        var webhookSubscriptionService = Substitute.For<IAkahuWebhookSubscriptionService>();
        var migrationJobService = Substitute.For<IAkahuMigrationJobService>();
        var logger = Substitute.For<IApplicationLogger<ExchangeAkahuCodeQueryHandler>>();

        webhookSubscriptionService.EnsureSubscriptionsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new EnsureSubscriptionsResult(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), new Dictionary<string, string>()));

        oauthStateStore.ValidateAndConsumeAsync(userId, "state-abc-123", Arg.Any<CancellationToken>())
            .Returns(true);

        akahuApiClient.ExchangeCodeForTokenAsync("code-456", Arg.Any<CancellationToken>())
            .Returns(new AkahuTokenResponse
            {
                AccessToken = "user_token_oauth",
                TokenType = "Bearer",
                Scope = "ENDURING_CONSENT"
            });

        akahuApiClient.GetAccountsWithCredentialsAsync("app_token_123", "user_token_oauth", Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AkahuAccountInfo>());

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
            oauthStateStore,
            webhookSubscriptionService,
            migrationJobService,
            logger);

        await handler.Handle(
            new ExchangeAkahuCodeQuery(userId, "code-456", "state-abc-123", "app_token_123"),
            CancellationToken.None);

        await credentialRepository.Received(1).AddAsync(
            Arg.Is<AkahuUserCredential>(c =>
                c.ConsentScope == "ENDURING_CONSENT" &&
                c.ConsentGrantedAt.HasValue &&
                c.ConsentCorrelationId == "state-abc-123"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OAuthCallback_EnqueuesMigrationJobOnlyForUnmigratedActiveConnections()
    {
        var userId = Guid.NewGuid();
        var akahuApiClient = Substitute.For<IAkahuApiClient>();
        var credentialRepository = Substitute.For<IAkahuUserCredentialRepository>();
        var bankConnectionRepository = Substitute.For<IBankConnectionRepository>();
        var encryptionService = Substitute.For<ISettingsEncryptionService>();
        var oauthStateStore = Substitute.For<IOAuthStateStore>();
        var webhookSubscriptionService = Substitute.For<IAkahuWebhookSubscriptionService>();
        var migrationJobService = Substitute.For<IAkahuMigrationJobService>();
        var logger = Substitute.For<IApplicationLogger<ExchangeAkahuCodeQueryHandler>>();

        webhookSubscriptionService.EnsureSubscriptionsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new EnsureSubscriptionsResult(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), new Dictionary<string, string>()));

        oauthStateStore.ValidateAndConsumeAsync(userId, "valid-state", Arg.Any<CancellationToken>()).Returns(true);
        akahuApiClient.ExchangeCodeForTokenAsync("code-xyz", Arg.Any<CancellationToken>())
            .Returns(new AkahuTokenResponse { AccessToken = "user_token_oauth", TokenType = "Bearer", Scope = "ENDURING_CONSENT" });
        akahuApiClient.GetAccountsWithCredentialsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AkahuAccountInfo>());

        credentialRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((AkahuUserCredential?)null);

        var alreadyMigrated = new BankConnection
        {
            Id = 1, UserId = userId, ProviderId = "akahu", IsActive = true,
            ExternalAccountId = "acc_new", LastMigratedAt = DateTime.UtcNow.AddDays(-1)
        };
        var inactive = new BankConnection
        {
            Id = 2, UserId = userId, ProviderId = "akahu", IsActive = false,
            ExternalAccountId = "acc_inactive"
        };
        var pending = new BankConnection
        {
            Id = 3, UserId = userId, ProviderId = "akahu", IsActive = true,
            ExternalAccountId = "acc_old", LastMigratedAt = null
        };
        var notAkahu = new BankConnection
        {
            Id = 4, UserId = userId, ProviderId = "emailforward", IsActive = true
        };

        bankConnectionRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new[] { alreadyMigrated, inactive, pending, notAkahu });

        encryptionService.EncryptSettings(Arg.Any<string>()).Returns("enc");

        var handler = new ExchangeAkahuCodeQueryHandler(
            akahuApiClient, credentialRepository, bankConnectionRepository,
            encryptionService, oauthStateStore, webhookSubscriptionService,
            migrationJobService, logger);

        await handler.Handle(new ExchangeAkahuCodeQuery(userId, "code-xyz", "valid-state", "app_token_123"), CancellationToken.None);

        // Only the active, unmigrated, akahu connection should have been enqueued.
        migrationJobService.Received(1).EnqueueMigration(userId, 3);
        migrationJobService.DidNotReceive().EnqueueMigration(userId, 1);
        migrationJobService.DidNotReceive().EnqueueMigration(userId, 2);
        migrationJobService.DidNotReceive().EnqueueMigration(userId, 4);
    }

    [Fact]
    public async Task Handle_NullState_ThrowsArgumentException()
    {
        var userId = Guid.NewGuid();
        var handler = CreateHandler();

        var act = () => handler.Handle(
            new ExchangeAkahuCodeQuery(userId, "code-123", null, "app_token_123"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*state*");
    }

    [Fact]
    public async Task Handle_InvalidState_ThrowsUnauthorizedAccessException()
    {
        var userId = Guid.NewGuid();
        var oauthStateStore = Substitute.For<IOAuthStateStore>();
        oauthStateStore.ValidateAndConsumeAsync(userId, "wrong-state", Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = CreateHandler(oauthStateStore: oauthStateStore);

        var act = () => handler.Handle(
            new ExchangeAkahuCodeQuery(userId, "code-123", "wrong-state", "app_token_123"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*expired*");
    }

    private static ExchangeAkahuCodeQueryHandler CreateHandler(IOAuthStateStore? oauthStateStore = null)
    {
        var webhookSubscriptionService = Substitute.For<IAkahuWebhookSubscriptionService>();
        webhookSubscriptionService.EnsureSubscriptionsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new EnsureSubscriptionsResult(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), new Dictionary<string, string>()));

        return new ExchangeAkahuCodeQueryHandler(
            Substitute.For<IAkahuApiClient>(),
            Substitute.For<IAkahuUserCredentialRepository>(),
            Substitute.For<IBankConnectionRepository>(),
            Substitute.For<ISettingsEncryptionService>(),
            oauthStateStore ?? Substitute.For<IOAuthStateStore>(),
            webhookSubscriptionService,
            Substitute.For<IAkahuMigrationJobService>(),
            Substitute.For<IApplicationLogger<ExchangeAkahuCodeQueryHandler>>());
    }
}
