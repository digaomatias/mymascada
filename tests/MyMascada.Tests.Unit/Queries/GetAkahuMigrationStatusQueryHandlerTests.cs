using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.Queries;
using MyMascada.Domain.Entities;
using NSubstitute.ExceptionExtensions;

namespace MyMascada.Tests.Unit.Queries;

public class GetAkahuMigrationStatusQueryHandlerTests
{
    private const string OldAccountId = "acc_OLD_xxx";
    private const string NewAccountId = "acc_NEW_yyy";

    [Fact]
    public async Task Handle_NoAkahuConnections_ReturnsEmptyList()
    {
        var ctx = CreateContext();
        var userId = Guid.NewGuid();

        ctx.BankConnectionRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<BankConnection>());

        var result = await ctx.Handler.Handle(new GetAkahuMigrationStatusQuery(userId), CancellationToken.None);

        result.PendingConnections.Should().BeEmpty();
        result.Deadline.Year.Should().Be(2026);
        result.Deadline.Month.Should().Be(5);
        result.Deadline.Day.Should().Be(24);

        await ctx.AkahuApiClient.DidNotReceive().GetAccountsWithCredentialsAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PendingMigration_ReturnsConnectionRow()
    {
        var ctx = CreateContext();
        var userId = Guid.NewGuid();

        var connection = new BankConnection
        {
            Id = 11,
            UserId = userId,
            ProviderId = "akahu",
            IsActive = true,
            ExternalAccountId = OldAccountId,
            ExternalAccountName = "ANZ Everyday",
            LastSyncAt = new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc)
        };

        ctx.BankConnectionRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new[] { connection });

        ctx.CredentialRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new AkahuUserCredential
            {
                Id = 1,
                UserId = userId,
                EncryptedAppToken = "enc_app",
                EncryptedUserToken = "enc_user"
            });

        ctx.EncryptionService.DecryptSettings<string>("enc_app").Returns("app_token");
        ctx.EncryptionService.DecryptSettings<string>("enc_user").Returns("user_token");

        ctx.AkahuApiClient.GetAccountsWithCredentialsAsync("app_token", "user_token", Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new AkahuAccountInfo
                {
                    Id = NewAccountId,
                    Name = "ANZ Everyday (Official)",
                    Migrated = OldAccountId,
                    BankName = "ANZ"
                }
            });

        var result = await ctx.Handler.Handle(new GetAkahuMigrationStatusQuery(userId), CancellationToken.None);

        result.PendingConnections.Should().HaveCount(1);
        var pending = result.PendingConnections[0];
        pending.ConnectionId.Should().Be(11);
        pending.ExternalAccountId.Should().Be(OldAccountId);
        pending.BankName.Should().Be("ANZ Everyday");
        pending.LastSyncedAt.Should().Be(connection.LastSyncAt);
    }

    [Fact]
    public async Task Handle_ConnectionAlreadyMigrated_ExcludedFromPending_NoApiProbe()
    {
        var ctx = CreateContext();
        var userId = Guid.NewGuid();

        var connection = new BankConnection
        {
            Id = 11,
            UserId = userId,
            ProviderId = "akahu",
            IsActive = true,
            ExternalAccountId = NewAccountId,
            ExternalAccountName = "ANZ Everyday",
            LastMigratedAt = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc)
        };

        ctx.BankConnectionRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new[] { connection });

        var result = await ctx.Handler.Handle(new GetAkahuMigrationStatusQuery(userId), CancellationToken.None);

        result.PendingConnections.Should().BeEmpty();

        // Already-migrated connections are excluded up front, so the Akahu API probe is skipped.
        await ctx.CredentialRepository.DidNotReceive().GetByUserIdAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await ctx.AkahuApiClient.DidNotReceive().GetAccountsWithCredentialsAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OneMigratedOneUnmigrated_OnlyUnmigratedConsidered()
    {
        var ctx = CreateContext();
        var userId = Guid.NewGuid();

        var migrated = new BankConnection
        {
            Id = 1,
            UserId = userId,
            ProviderId = "akahu",
            IsActive = true,
            ExternalAccountId = "acc_already_migrated",
            LastMigratedAt = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc)
        };
        var pending = new BankConnection
        {
            Id = 2,
            UserId = userId,
            ProviderId = "akahu",
            IsActive = true,
            ExternalAccountId = OldAccountId,
            ExternalAccountName = "ANZ Everyday",
            LastMigratedAt = null
        };

        ctx.BankConnectionRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new[] { migrated, pending });

        ctx.CredentialRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new AkahuUserCredential
            {
                Id = 1,
                UserId = userId,
                EncryptedAppToken = "enc_app",
                EncryptedUserToken = "enc_user"
            });

        ctx.EncryptionService.DecryptSettings<string>("enc_app").Returns("app_token");
        ctx.EncryptionService.DecryptSettings<string>("enc_user").Returns("user_token");

        ctx.AkahuApiClient.GetAccountsWithCredentialsAsync("app_token", "user_token", Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new AkahuAccountInfo { Id = NewAccountId, Migrated = OldAccountId }
            });

        var result = await ctx.Handler.Handle(new GetAkahuMigrationStatusQuery(userId), CancellationToken.None);

        result.PendingConnections.Should().HaveCount(1);
        result.PendingConnections[0].ConnectionId.Should().Be(2);
    }

    [Fact]
    public async Task Handle_NoPendingMigration_ReturnsEmptyList()
    {
        var ctx = CreateContext();
        var userId = Guid.NewGuid();

        var connection = new BankConnection
        {
            Id = 7,
            UserId = userId,
            ProviderId = "akahu",
            IsActive = true,
            ExternalAccountId = OldAccountId
        };

        ctx.BankConnectionRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new[] { connection });

        ctx.CredentialRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new AkahuUserCredential
            {
                Id = 1,
                UserId = userId,
                EncryptedAppToken = "enc_app",
                EncryptedUserToken = "enc_user"
            });

        ctx.EncryptionService.DecryptSettings<string>("enc_app").Returns("app_token");
        ctx.EncryptionService.DecryptSettings<string>("enc_user").Returns("user_token");

        // None of the returned accounts carry a _migrated pointing at the user's classic account
        ctx.AkahuApiClient.GetAccountsWithCredentialsAsync("app_token", "user_token", Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new AkahuAccountInfo { Id = "acc_other", Migrated = null }
            });

        var result = await ctx.Handler.Handle(new GetAkahuMigrationStatusQuery(userId), CancellationToken.None);

        result.PendingConnections.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_AkahuApiUnauthorized_ReturnsEmptyListGracefully()
    {
        var ctx = CreateContext();
        var userId = Guid.NewGuid();

        var connection = new BankConnection
        {
            Id = 7,
            UserId = userId,
            ProviderId = "akahu",
            IsActive = true,
            ExternalAccountId = OldAccountId
        };

        ctx.BankConnectionRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new[] { connection });

        ctx.CredentialRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new AkahuUserCredential
            {
                Id = 1,
                UserId = userId,
                EncryptedAppToken = "enc_app",
                EncryptedUserToken = "enc_user"
            });

        ctx.EncryptionService.DecryptSettings<string>("enc_app").Returns("app_token");
        ctx.EncryptionService.DecryptSettings<string>("enc_user").Returns("user_token");

        ctx.AkahuApiClient.GetAccountsWithCredentialsAsync("app_token", "user_token", Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedAccessException("token expired"));

        var result = await ctx.Handler.Handle(new GetAkahuMigrationStatusQuery(userId), CancellationToken.None);

        result.PendingConnections.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_AwaitingReauthInactiveConnection_StillSurfacedInPendingList()
    {
        // Regression: a connection that the migrate command disabled with
        // LastSyncError = "Awaiting re-authorisation" must keep showing up in the
        // banner. Filtering on IsActive == true alone would silently hide the row
        // exactly when the user needs to act.
        var ctx = CreateContext();
        var userId = Guid.NewGuid();

        var connection = new BankConnection
        {
            Id = 22,
            UserId = userId,
            ProviderId = "akahu",
            IsActive = false,
            LastSyncError = "Awaiting re-authorisation",
            ExternalAccountId = OldAccountId,
            ExternalAccountName = "ANZ Everyday"
        };

        ctx.BankConnectionRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new[] { connection });

        ctx.CredentialRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new AkahuUserCredential
            {
                Id = 1,
                UserId = userId,
                EncryptedAppToken = "enc_app",
                EncryptedUserToken = "enc_user"
            });

        ctx.EncryptionService.DecryptSettings<string>("enc_app").Returns("app_token");
        ctx.EncryptionService.DecryptSettings<string>("enc_user").Returns("user_token");

        ctx.AkahuApiClient.GetAccountsWithCredentialsAsync("app_token", "user_token", Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new AkahuAccountInfo { Id = NewAccountId, Migrated = OldAccountId, Name = "ANZ Everyday (Official)" }
            });

        var result = await ctx.Handler.Handle(new GetAkahuMigrationStatusQuery(userId), CancellationToken.None);

        result.PendingConnections.Should().HaveCount(1);
        result.PendingConnections[0].ConnectionId.Should().Be(22);
    }

    [Fact]
    public async Task Handle_CredentialMissing_ReturnsEmptyList()
    {
        var ctx = CreateContext();
        var userId = Guid.NewGuid();

        ctx.BankConnectionRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new BankConnection
                {
                    Id = 1,
                    UserId = userId,
                    ProviderId = "akahu",
                    IsActive = true,
                    ExternalAccountId = OldAccountId
                }
            });

        ctx.CredentialRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((AkahuUserCredential?)null);

        var result = await ctx.Handler.Handle(new GetAkahuMigrationStatusQuery(userId), CancellationToken.None);

        result.PendingConnections.Should().BeEmpty();
        await ctx.AkahuApiClient.DidNotReceive().GetAccountsWithCredentialsAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static HandlerContext CreateContext()
    {
        var bankConnectionRepository = Substitute.For<IBankConnectionRepository>();
        var credentialRepository = Substitute.For<IAkahuUserCredentialRepository>();
        var akahuApiClient = Substitute.For<IAkahuApiClient>();
        var encryptionService = Substitute.For<ISettingsEncryptionService>();
        var logger = Substitute.For<IApplicationLogger<GetAkahuMigrationStatusQueryHandler>>();

        var handler = new GetAkahuMigrationStatusQueryHandler(
            bankConnectionRepository,
            credentialRepository,
            akahuApiClient,
            encryptionService,
            logger);

        return new HandlerContext(handler, bankConnectionRepository, credentialRepository, akahuApiClient, encryptionService);
    }

    private sealed record HandlerContext(
        GetAkahuMigrationStatusQueryHandler Handler,
        IBankConnectionRepository BankConnectionRepository,
        IAkahuUserCredentialRepository CredentialRepository,
        IAkahuApiClient AkahuApiClient,
        ISettingsEncryptionService EncryptionService);
}
