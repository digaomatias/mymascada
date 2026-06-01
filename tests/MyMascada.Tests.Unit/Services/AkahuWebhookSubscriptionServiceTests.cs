using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.DTOs;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Services.BankIntegration.Providers;
using NSubstitute.ExceptionExtensions;

namespace MyMascada.Tests.Unit.Services;

public class AkahuWebhookSubscriptionServiceTests
{
    private const string AppToken = "app_token";
    private const string UserToken = "user_token";

    [Fact]
    public async Task EnsureSubscriptionsAsync_NoExistingSubscriptions_SubscribesAllThreeTypes()
    {
        var ctx = CreateContext();
        var userId = Guid.NewGuid();
        SetupHappyCredentials(ctx, userId);

        ctx.SubscriptionRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AkahuWebhookSubscription>());

        ctx.AkahuApiClient.ListWebhooksAsync(AppToken, UserToken, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AkahuWebhookSubscriptionInfo>());

        SetupSubscribeReturns(ctx);

        var result = await ctx.Service.EnsureSubscriptionsAsync(userId);

        result.SubscribedTypes.Should().BeEquivalentTo(new[]
        {
            AkahuWebhookTypes.Token,
            AkahuWebhookTypes.Account,
            AkahuWebhookTypes.Transaction
        });
        result.AdoptedTypes.Should().BeEmpty();
        result.AlreadyHealthyTypes.Should().BeEmpty();
        result.FailedTypes.Should().BeEmpty();

        await ctx.SubscriptionRepository.Received(3).AddAsync(
            Arg.Any<AkahuWebhookSubscription>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureSubscriptionsAsync_OneTypeAlreadySubscribed_SubscribesOnlyMissing()
    {
        var ctx = CreateContext();
        var userId = Guid.NewGuid();
        SetupHappyCredentials(ctx, userId);

        var existing = new AkahuWebhookSubscription
        {
            Id = 10,
            UserId = userId,
            AkahuUserCredentialId = 1,
            WebhookId = "whk_token",
            WebhookType = AkahuWebhookTypes.Token,
            State = userId.ToString("N")
        };

        ctx.SubscriptionRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new[] { existing });

        ctx.AkahuApiClient.ListWebhooksAsync(AppToken, UserToken, Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new AkahuWebhookSubscriptionInfo { Id = "whk_token", WebhookType = AkahuWebhookTypes.Token, State = userId.ToString("N") }
            });

        SetupSubscribeReturns(ctx);

        var result = await ctx.Service.EnsureSubscriptionsAsync(userId);

        result.AlreadyHealthyTypes.Should().Contain(AkahuWebhookTypes.Token);
        result.SubscribedTypes.Should().BeEquivalentTo(new[] { AkahuWebhookTypes.Account, AkahuWebhookTypes.Transaction });
        result.FailedTypes.Should().BeEmpty();

        await ctx.AkahuApiClient.DidNotReceive().SubscribeToWebhookAsync(
            AppToken, UserToken, AkahuWebhookTypes.Token, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureSubscriptionsAsync_AllPresentInAkahuButNotLocal_AdoptsRowsWithoutPosting()
    {
        var ctx = CreateContext();
        var userId = Guid.NewGuid();
        SetupHappyCredentials(ctx, userId);

        ctx.SubscriptionRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AkahuWebhookSubscription>());

        ctx.AkahuApiClient.ListWebhooksAsync(AppToken, UserToken, Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new AkahuWebhookSubscriptionInfo { Id = "whk_token", WebhookType = AkahuWebhookTypes.Token, State = userId.ToString("N") },
                new AkahuWebhookSubscriptionInfo { Id = "whk_acc", WebhookType = AkahuWebhookTypes.Account, State = userId.ToString("N") },
                new AkahuWebhookSubscriptionInfo { Id = "whk_tx", WebhookType = AkahuWebhookTypes.Transaction, State = userId.ToString("N") }
            });

        var result = await ctx.Service.EnsureSubscriptionsAsync(userId);

        result.AdoptedTypes.Should().BeEquivalentTo(new[]
        {
            AkahuWebhookTypes.Token,
            AkahuWebhookTypes.Account,
            AkahuWebhookTypes.Transaction
        });
        result.SubscribedTypes.Should().BeEmpty();

        await ctx.AkahuApiClient.DidNotReceive().SubscribeToWebhookAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        await ctx.SubscriptionRepository.Received(3).AddAsync(Arg.Any<AkahuWebhookSubscription>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureSubscriptionsAsync_TransactionSubscribeFails_OtherTwoStillPersisted()
    {
        var ctx = CreateContext();
        var userId = Guid.NewGuid();
        SetupHappyCredentials(ctx, userId);

        ctx.SubscriptionRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AkahuWebhookSubscription>());

        ctx.AkahuApiClient.ListWebhooksAsync(AppToken, UserToken, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AkahuWebhookSubscriptionInfo>());

        ctx.AkahuApiClient.SubscribeToWebhookAsync(AppToken, UserToken, AkahuWebhookTypes.Token, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AkahuWebhookSubscriptionInfo { Id = "whk_token", WebhookType = AkahuWebhookTypes.Token });

        ctx.AkahuApiClient.SubscribeToWebhookAsync(AppToken, UserToken, AkahuWebhookTypes.Account, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AkahuWebhookSubscriptionInfo { Id = "whk_acc", WebhookType = AkahuWebhookTypes.Account });

        ctx.AkahuApiClient.SubscribeToWebhookAsync(AppToken, UserToken, AkahuWebhookTypes.Transaction, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Service Unavailable"));

        var result = await ctx.Service.EnsureSubscriptionsAsync(userId);

        result.SubscribedTypes.Should().BeEquivalentTo(new[] { AkahuWebhookTypes.Token, AkahuWebhookTypes.Account });
        result.FailedTypes.Should().ContainKey(AkahuWebhookTypes.Transaction);

        await ctx.SubscriptionRepository.Received(2).AddAsync(Arg.Any<AkahuWebhookSubscription>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureSubscriptionsAsync_CredentialNotFound_NoOp()
    {
        var ctx = CreateContext();
        var userId = Guid.NewGuid();

        ctx.CredentialRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((AkahuUserCredential?)null);

        var result = await ctx.Service.EnsureSubscriptionsAsync(userId);

        result.SubscribedTypes.Should().BeEmpty();
        result.AdoptedTypes.Should().BeEmpty();
        result.AlreadyHealthyTypes.Should().BeEmpty();
        result.FailedTypes.Should().BeEmpty();

        await ctx.AkahuApiClient.DidNotReceive().SubscribeToWebhookAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureSubscriptionsAsync_RegistersStateAsUserGuid()
    {
        var ctx = CreateContext();
        var userId = Guid.NewGuid();
        SetupHappyCredentials(ctx, userId);

        ctx.SubscriptionRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AkahuWebhookSubscription>());

        ctx.AkahuApiClient.ListWebhooksAsync(AppToken, UserToken, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AkahuWebhookSubscriptionInfo>());

        SetupSubscribeReturns(ctx);

        await ctx.Service.EnsureSubscriptionsAsync(userId);

        await ctx.AkahuApiClient.Received(1).SubscribeToWebhookAsync(
            AppToken, UserToken, AkahuWebhookTypes.Token, userId.ToString("N"), Arg.Any<CancellationToken>());
        await ctx.AkahuApiClient.Received(1).SubscribeToWebhookAsync(
            AppToken, UserToken, AkahuWebhookTypes.Account, userId.ToString("N"), Arg.Any<CancellationToken>());
        await ctx.AkahuApiClient.Received(1).SubscribeToWebhookAsync(
            AppToken, UserToken, AkahuWebhookTypes.Transaction, userId.ToString("N"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TearDownSubscriptionsAsync_CallsDeleteForEachRow_ThenDeletesLocalRows()
    {
        var ctx = CreateContext();
        var userId = Guid.NewGuid();
        SetupHappyCredentials(ctx, userId);

        ctx.SubscriptionRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new AkahuWebhookSubscription { Id = 1, UserId = userId, AkahuUserCredentialId = 1, WebhookId = "whk_a", WebhookType = AkahuWebhookTypes.Token },
                new AkahuWebhookSubscription { Id = 2, UserId = userId, AkahuUserCredentialId = 1, WebhookId = "whk_b", WebhookType = AkahuWebhookTypes.Account },
                new AkahuWebhookSubscription { Id = 3, UserId = userId, AkahuUserCredentialId = 1, WebhookId = "whk_c", WebhookType = AkahuWebhookTypes.Transaction }
            });

        await ctx.Service.TearDownSubscriptionsAsync(userId);

        await ctx.AkahuApiClient.Received(1).UnsubscribeFromWebhookAsync(AppToken, UserToken, "whk_a", Arg.Any<CancellationToken>());
        await ctx.AkahuApiClient.Received(1).UnsubscribeFromWebhookAsync(AppToken, UserToken, "whk_b", Arg.Any<CancellationToken>());
        await ctx.AkahuApiClient.Received(1).UnsubscribeFromWebhookAsync(AppToken, UserToken, "whk_c", Arg.Any<CancellationToken>());

        await ctx.SubscriptionRepository.Received(1).DeleteByUserIdAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TearDownSubscriptionsAsync_AkahuUnauthorized_DeletesLocalRowsAnyway()
    {
        var ctx = CreateContext();
        var userId = Guid.NewGuid();
        SetupHappyCredentials(ctx, userId);

        ctx.SubscriptionRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new AkahuWebhookSubscription { Id = 1, UserId = userId, AkahuUserCredentialId = 1, WebhookId = "whk_a", WebhookType = AkahuWebhookTypes.Token }
            });

        ctx.AkahuApiClient.UnsubscribeFromWebhookAsync(AppToken, UserToken, "whk_a", Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedAccessException("expired"));

        await ctx.Service.TearDownSubscriptionsAsync(userId);

        await ctx.SubscriptionRepository.Received(1).DeleteByUserIdAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureSubscriptionsAsync_ListWebhooksThrows_AbortsWithoutDeletingLocalRows()
    {
        // Regression: treating an unreachable Akahu as "remote has no subscriptions" used
        // to tear down perfectly-good local rows. We must abort the reconcile and let the
        // next cycle pick it up instead.
        var ctx = CreateContext();
        var userId = Guid.NewGuid();
        SetupHappyCredentials(ctx, userId);

        var localToken = new AkahuWebhookSubscription
        {
            Id = 7,
            UserId = userId,
            AkahuUserCredentialId = 1,
            WebhookId = "whk_existing_token",
            WebhookType = AkahuWebhookTypes.Token
        };
        ctx.SubscriptionRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new[] { localToken });

        ctx.AkahuApiClient.ListWebhooksAsync(AppToken, UserToken, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Akahu unreachable"));

        var result = await ctx.Service.EnsureSubscriptionsAsync(userId);

        // Every required webhook type is reported as failed so callers can surface the issue.
        result.FailedTypes.Should().NotBeEmpty();
        // No destructive operations should have run.
        await ctx.SubscriptionRepository.DidNotReceive().DeleteByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await ctx.SubscriptionRepository.DidNotReceive().DeleteByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await ctx.AkahuApiClient.DidNotReceive().SubscribeToWebhookAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureSubscriptionsAsync_LocalRowMissingAtAkahu_ReSubscribes()
    {
        var ctx = CreateContext();
        var userId = Guid.NewGuid();
        SetupHappyCredentials(ctx, userId);

        ctx.SubscriptionRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new AkahuWebhookSubscription { Id = 5, UserId = userId, AkahuUserCredentialId = 1, WebhookId = "whk_stale_token", WebhookType = AkahuWebhookTypes.Token }
            });

        ctx.AkahuApiClient.ListWebhooksAsync(AppToken, UserToken, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AkahuWebhookSubscriptionInfo>());

        SetupSubscribeReturns(ctx);

        var result = await ctx.Service.EnsureSubscriptionsAsync(userId);

        result.SubscribedTypes.Should().Contain(AkahuWebhookTypes.Token);
        await ctx.SubscriptionRepository.Received(1).DeleteByIdAsync(5, Arg.Any<CancellationToken>());
        await ctx.AkahuApiClient.Received(1).SubscribeToWebhookAsync(
            AppToken, UserToken, AkahuWebhookTypes.Token, userId.ToString("N"), Arg.Any<CancellationToken>());
    }

    private static void SetupHappyCredentials(Context ctx, Guid userId)
    {
        ctx.CredentialRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new AkahuUserCredential
            {
                Id = 1,
                UserId = userId,
                EncryptedAppToken = "enc_app",
                EncryptedUserToken = "enc_user"
            });

        ctx.EncryptionService.DecryptSettings<string>("enc_app").Returns(AppToken);
        ctx.EncryptionService.DecryptSettings<string>("enc_user").Returns(UserToken);
    }

    private static void SetupSubscribeReturns(Context ctx)
    {
        ctx.AkahuApiClient.SubscribeToWebhookAsync(AppToken, UserToken, AkahuWebhookTypes.Token, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AkahuWebhookSubscriptionInfo { Id = "whk_token_new", WebhookType = AkahuWebhookTypes.Token });
        ctx.AkahuApiClient.SubscribeToWebhookAsync(AppToken, UserToken, AkahuWebhookTypes.Account, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AkahuWebhookSubscriptionInfo { Id = "whk_acc_new", WebhookType = AkahuWebhookTypes.Account });
        ctx.AkahuApiClient.SubscribeToWebhookAsync(AppToken, UserToken, AkahuWebhookTypes.Transaction, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AkahuWebhookSubscriptionInfo { Id = "whk_tx_new", WebhookType = AkahuWebhookTypes.Transaction });
    }

    private static Context CreateContext()
    {
        var akahuApiClient = Substitute.For<IAkahuApiClient>();
        var credentialRepository = Substitute.For<IAkahuUserCredentialRepository>();
        var subscriptionRepository = Substitute.For<IAkahuWebhookSubscriptionRepository>();
        var encryptionService = Substitute.For<ISettingsEncryptionService>();
        var logger = Substitute.For<IApplicationLogger<AkahuWebhookSubscriptionService>>();

        var service = new AkahuWebhookSubscriptionService(
            akahuApiClient,
            credentialRepository,
            subscriptionRepository,
            encryptionService,
            logger);

        return new Context(service, akahuApiClient, credentialRepository, subscriptionRepository, encryptionService);
    }

    private sealed record Context(
        AkahuWebhookSubscriptionService Service,
        IAkahuApiClient AkahuApiClient,
        IAkahuUserCredentialRepository CredentialRepository,
        IAkahuWebhookSubscriptionRepository SubscriptionRepository,
        ISettingsEncryptionService EncryptionService);
}
