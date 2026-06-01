using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.Commands;
using MyMascada.Application.Features.BankConnections.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Tests.Unit.Commands;

public class MigrateAkahuConnectionCommandHandlerTests
{
    private const string OldAccountId = "acc_OLD_123";
    private const string NewAccountId = "acc_NEW_456";
    private const string OldTxId1 = "trans_OLD_aaa";
    private const string NewTxId1 = "trans_NEW_aaa";
    private const string OldTxId2 = "trans_OLD_bbb";
    private const string NewTxId2 = "trans_NEW_bbb";

    [Fact]
    public async Task Handle_AccountFoundInMigrationList_UpdatesExternalAccountIdAndTransactions()
    {
        var userId = Guid.NewGuid();
        var connectionId = 42;
        var accountId = 100;
        var ctx = CreateContext(userId, connectionId, accountId);

        ctx.AkahuApiClient.GetAccountsWithCredentialsAsync("app_token", "user_token", Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new AkahuAccountInfo
                {
                    Id = NewAccountId,
                    Name = "Everyday (Official)",
                    Migrated = OldAccountId,
                    Currency = "NZD"
                }
            });

        ctx.TransactionRepository.GetOldestTransactionDateForAccountAsync(accountId, Arg.Any<CancellationToken>())
            .Returns(DateTime.UtcNow.Date.AddDays(-30));

        ctx.AkahuApiClient.GetTransactionsWithCredentialsAsync(
                "app_token", "user_token", NewAccountId, Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new BankTransactionDto { ExternalId = NewTxId1, Migrated = OldTxId1, Amount = -10m, Description = "Coffee" },
                new BankTransactionDto { ExternalId = NewTxId2, Migrated = OldTxId2, Amount = -20m, Description = "Lunch" }
            });

        ctx.TransactionRepository.RemapExternalIdsAsync(accountId, Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(2);

        var handler = ctx.BuildHandler();

        var result = await handler.Handle(new MigrateAkahuConnectionCommand(userId, connectionId), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.OldExternalAccountId.Should().Be(OldAccountId);
        result.NewExternalAccountId.Should().Be(NewAccountId);
        result.TransactionsRemapped.Should().Be(2);
        result.ErrorMessage.Should().BeNull();

        await ctx.BankConnectionRepository.Received(1).UpdateAsync(
            Arg.Is<BankConnection>(c =>
                c.Id == connectionId &&
                c.ExternalAccountId == NewAccountId &&
                c.IsActive == true &&
                c.LastSyncError == null),
            Arg.Any<CancellationToken>());

        await ctx.TransactionRepository.Received(1).RemapExternalIdsAsync(
            accountId,
            Arg.Is<IReadOnlyDictionary<string, string>>(map =>
                map.Count == 2 &&
                map[OldTxId1] == NewTxId1 &&
                map[OldTxId2] == NewTxId2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoMatchingMigratedAccount_MarksConnectionAwaitingReauth_ReturnsFailure()
    {
        var userId = Guid.NewGuid();
        var connectionId = 42;
        var accountId = 100;
        var ctx = CreateContext(userId, connectionId, accountId);

        ctx.AkahuApiClient.GetAccountsWithCredentialsAsync("app_token", "user_token", Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new AkahuAccountInfo { Id = "acc_unrelated", Name = "Other", Migrated = "acc_somethingelse" },
                new AkahuAccountInfo { Id = "acc_no_migration", Name = "Native" }
            });

        var handler = ctx.BuildHandler();

        var result = await handler.Handle(new MigrateAkahuConnectionCommand(userId, connectionId), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.OldExternalAccountId.Should().Be(OldAccountId);
        result.NewExternalAccountId.Should().BeNull();
        result.TransactionsRemapped.Should().Be(0);
        result.ErrorMessage.Should().Contain("re-auth");

        await ctx.BankConnectionRepository.Received(1).UpdateAsync(
            Arg.Is<BankConnection>(c =>
                c.Id == connectionId &&
                c.ExternalAccountId == OldAccountId &&
                c.IsActive == false &&
                c.LastSyncError == "Awaiting re-authorisation"),
            Arg.Any<CancellationToken>());

        await ctx.TransactionRepository.DidNotReceive().RemapExternalIdsAsync(
            Arg.Any<int>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SuccessfulMigration_SetsLastMigratedAt()
    {
        var userId = Guid.NewGuid();
        var connectionId = 42;
        var accountId = 100;
        var ctx = CreateContext(userId, connectionId, accountId);

        ctx.Connection!.LastMigratedAt.Should().BeNull("connection starts unmigrated");

        ctx.AkahuApiClient.GetAccountsWithCredentialsAsync("app_token", "user_token", Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new AkahuAccountInfo { Id = NewAccountId, Name = "Everyday", Migrated = OldAccountId }
            });

        ctx.TransactionRepository.GetOldestTransactionDateForAccountAsync(accountId, Arg.Any<CancellationToken>())
            .Returns((DateTime?)null);

        ctx.AkahuApiClient.GetTransactionsWithCredentialsAsync(
                "app_token", "user_token", NewAccountId, Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<BankTransactionDto>());

        var before = DateTime.UtcNow;
        var handler = ctx.BuildHandler();

        var result = await handler.Handle(new MigrateAkahuConnectionCommand(userId, connectionId), CancellationToken.None);
        var after = DateTime.UtcNow;

        result.Success.Should().BeTrue();

        await ctx.BankConnectionRepository.Received(1).UpdateAsync(
            Arg.Is<BankConnection>(c =>
                c.LastMigratedAt != null &&
                c.LastMigratedAt >= before &&
                c.LastMigratedAt <= after),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoMatchingMigratedAccount_LeavesLastMigratedAtNull()
    {
        var userId = Guid.NewGuid();
        var connectionId = 42;
        var accountId = 100;
        var ctx = CreateContext(userId, connectionId, accountId);

        ctx.AkahuApiClient.GetAccountsWithCredentialsAsync("app_token", "user_token", Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new AkahuAccountInfo { Id = "acc_unrelated", Name = "Other", Migrated = "acc_somethingelse" }
            });

        var handler = ctx.BuildHandler();

        var result = await handler.Handle(new MigrateAkahuConnectionCommand(userId, connectionId), CancellationToken.None);

        result.Success.Should().BeFalse();

        await ctx.BankConnectionRepository.Received(1).UpdateAsync(
            Arg.Is<BankConnection>(c => c.LastMigratedAt == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_BankConnectionNotFound_ReturnsFailure()
    {
        var userId = Guid.NewGuid();
        var ctx = CreateContext(userId, 99, 100, includeConnection: false);

        var handler = ctx.BuildHandler();

        var result = await handler.Handle(new MigrateAkahuConnectionCommand(userId, 99), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.OldExternalAccountId.Should().BeNull();
        result.NewExternalAccountId.Should().BeNull();
        result.ErrorMessage.Should().Contain("not found");

        await ctx.BankConnectionRepository.DidNotReceive().UpdateAsync(Arg.Any<BankConnection>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_BankConnectionBelongsToDifferentUser_ReturnsFailure()
    {
        var requestingUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var connectionId = 77;
        var ctx = CreateContext(otherUserId, connectionId, 100);

        var handler = ctx.BuildHandler();

        var result = await handler.Handle(new MigrateAkahuConnectionCommand(requestingUserId, connectionId), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.NewExternalAccountId.Should().BeNull();
        result.ErrorMessage.Should().Contain("not found");

        await ctx.BankConnectionRepository.DidNotReceive().UpdateAsync(Arg.Any<BankConnection>(), Arg.Any<CancellationToken>());
        await ctx.AkahuApiClient.DidNotReceive().GetAccountsWithCredentialsAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoTransactionsToMigrate_StillSucceeds_RemappedCountZero()
    {
        var userId = Guid.NewGuid();
        var connectionId = 42;
        var accountId = 100;
        var ctx = CreateContext(userId, connectionId, accountId);

        ctx.AkahuApiClient.GetAccountsWithCredentialsAsync("app_token", "user_token", Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new AkahuAccountInfo { Id = NewAccountId, Name = "Everyday", Migrated = OldAccountId }
            });

        ctx.TransactionRepository.GetOldestTransactionDateForAccountAsync(accountId, Arg.Any<CancellationToken>())
            .Returns((DateTime?)null);

        ctx.AkahuApiClient.GetTransactionsWithCredentialsAsync(
                "app_token", "user_token", NewAccountId, Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<BankTransactionDto>());

        var handler = ctx.BuildHandler();

        var result = await handler.Handle(new MigrateAkahuConnectionCommand(userId, connectionId), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.OldExternalAccountId.Should().Be(OldAccountId);
        result.NewExternalAccountId.Should().Be(NewAccountId);
        result.TransactionsRemapped.Should().Be(0);

        await ctx.BankConnectionRepository.Received(1).UpdateAsync(
            Arg.Is<BankConnection>(c => c.ExternalAccountId == NewAccountId),
            Arg.Any<CancellationToken>());

        await ctx.TransactionRepository.DidNotReceive().RemapExternalIdsAsync(
            Arg.Any<int>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TransactionMigratedFieldNull_SkipsThatTransaction()
    {
        var userId = Guid.NewGuid();
        var connectionId = 42;
        var accountId = 100;
        var ctx = CreateContext(userId, connectionId, accountId);

        ctx.AkahuApiClient.GetAccountsWithCredentialsAsync("app_token", "user_token", Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new AkahuAccountInfo { Id = NewAccountId, Name = "Everyday", Migrated = OldAccountId }
            });

        ctx.TransactionRepository.GetOldestTransactionDateForAccountAsync(accountId, Arg.Any<CancellationToken>())
            .Returns(DateTime.UtcNow.Date.AddDays(-30));

        ctx.AkahuApiClient.GetTransactionsWithCredentialsAsync(
                "app_token", "user_token", NewAccountId, Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new BankTransactionDto { ExternalId = NewTxId1, Migrated = OldTxId1, Amount = -10m, Description = "Coffee" },
                new BankTransactionDto { ExternalId = "trans_NEW_native", Migrated = null, Amount = -5m, Description = "Native (not migrated)" }
            });

        ctx.TransactionRepository.RemapExternalIdsAsync(
                accountId, Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(1);

        var handler = ctx.BuildHandler();

        var result = await handler.Handle(new MigrateAkahuConnectionCommand(userId, connectionId), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TransactionsRemapped.Should().Be(1);

        await ctx.TransactionRepository.Received(1).RemapExternalIdsAsync(
            accountId,
            Arg.Is<IReadOnlyDictionary<string, string>>(map =>
                map.Count == 1 && map.ContainsKey(OldTxId1) && map[OldTxId1] == NewTxId1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EncryptedSettingsDecryptionFails_MarksConnectionAndSkipsAkahuCalls()
    {
        var userId = Guid.NewGuid();
        var connectionId = 42;
        var accountId = 100;
        var ctx = CreateContext(userId, connectionId, accountId);

        ctx.Connection!.EncryptedSettings = "ENC_BROKEN";

        ctx.EncryptionService
            .DecryptSettings<AkahuConnectionSettings>("ENC_BROKEN")
            .Returns<AkahuConnectionSettings?>(_ => throw new InvalidOperationException("Data Protection keys missing"));

        var handler = ctx.BuildHandler();

        var result = await handler.Handle(new MigrateAkahuConnectionCommand(userId, connectionId), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("settings");

        await ctx.BankConnectionRepository.Received(1).UpdateAsync(
            Arg.Is<BankConnection>(c =>
                c.Id == connectionId &&
                c.IsActive == false &&
                c.LastSyncError != null &&
                c.LastSyncError.Contains("re-link")),
            Arg.Any<CancellationToken>());

        // Should not have called the Akahu accounts/transactions endpoints — fail fast.
        await ctx.AkahuApiClient.DidNotReceive().GetAccountsWithCredentialsAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await ctx.AkahuApiClient.DidNotReceive().GetTransactionsWithCredentialsAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SuccessfulMigration_CommitsUnitOfWorkTransaction()
    {
        var userId = Guid.NewGuid();
        var connectionId = 42;
        var accountId = 100;
        var ctx = CreateContext(userId, connectionId, accountId);

        ctx.AkahuApiClient.GetAccountsWithCredentialsAsync("app_token", "user_token", Arg.Any<CancellationToken>())
            .Returns(new[] { new AkahuAccountInfo { Id = NewAccountId, Name = "Everyday", Migrated = OldAccountId } });
        ctx.TransactionRepository.GetOldestTransactionDateForAccountAsync(accountId, Arg.Any<CancellationToken>())
            .Returns((DateTime?)null);
        ctx.AkahuApiClient.GetTransactionsWithCredentialsAsync(
                "app_token", "user_token", NewAccountId, Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<BankTransactionDto>());

        var handler = ctx.BuildHandler();
        var result = await handler.Handle(new MigrateAkahuConnectionCommand(userId, connectionId), CancellationToken.None);

        result.Success.Should().BeTrue();
        await ctx.UnitOfWork.Received(1).BeginTransactionAsync(Arg.Any<CancellationToken>());
        await ctx.Transaction.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RemapThrows_TransactionNotCommitted()
    {
        var userId = Guid.NewGuid();
        var connectionId = 42;
        var accountId = 100;
        var ctx = CreateContext(userId, connectionId, accountId);

        ctx.AkahuApiClient.GetAccountsWithCredentialsAsync("app_token", "user_token", Arg.Any<CancellationToken>())
            .Returns(new[] { new AkahuAccountInfo { Id = NewAccountId, Name = "Everyday", Migrated = OldAccountId } });
        ctx.TransactionRepository.GetOldestTransactionDateForAccountAsync(accountId, Arg.Any<CancellationToken>())
            .Returns(DateTime.UtcNow.Date.AddDays(-30));
        ctx.AkahuApiClient.GetTransactionsWithCredentialsAsync(
                "app_token", "user_token", NewAccountId, Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new BankTransactionDto { ExternalId = NewTxId1, Migrated = OldTxId1, Amount = -10m, Description = "Coffee" }
            });

        ctx.TransactionRepository.RemapExternalIdsAsync(accountId, Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new InvalidOperationException("simulated remap failure"));

        var handler = ctx.BuildHandler();

        var act = async () => await handler.Handle(new MigrateAkahuConnectionCommand(userId, connectionId), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();

        await ctx.UnitOfWork.Received(1).BeginTransactionAsync(Arg.Any<CancellationToken>());
        await ctx.Transaction.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await ctx.Transaction.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task Handle_EncryptedSettingsRoundTrip_AkahuAccountIdRewrittenInSettingsToo()
    {
        var userId = Guid.NewGuid();
        var connectionId = 42;
        var accountId = 100;
        var ctx = CreateContext(userId, connectionId, accountId);

        ctx.Connection!.EncryptedSettings = "ENC_OLD_SETTINGS";

        // Settings round-trip: when the handler decrypts the existing connection settings, we
        // return an instance with a stale AkahuAccountId so the test can assert that it gets
        // rewritten with the new ID and re-encrypted.
        ctx.EncryptionService
            .DecryptSettings<AkahuConnectionSettings>("ENC_OLD_SETTINGS")
            .Returns(new AkahuConnectionSettings { AkahuAccountId = OldAccountId });

        var capturedEncrypt = new List<object>();
        ctx.EncryptionService
            .EncryptSettings(Arg.Do<object>(o => capturedEncrypt.Add(o)))
            .Returns("ENC_NEW_SETTINGS");

        ctx.AkahuApiClient.GetAccountsWithCredentialsAsync("app_token", "user_token", Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new AkahuAccountInfo { Id = NewAccountId, Name = "Everyday", Migrated = OldAccountId }
            });

        ctx.TransactionRepository.GetOldestTransactionDateForAccountAsync(accountId, Arg.Any<CancellationToken>())
            .Returns((DateTime?)null);

        ctx.AkahuApiClient.GetTransactionsWithCredentialsAsync(
                "app_token", "user_token", NewAccountId, Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<BankTransactionDto>());

        var handler = ctx.BuildHandler();

        var result = await handler.Handle(new MigrateAkahuConnectionCommand(userId, connectionId), CancellationToken.None);

        result.Success.Should().BeTrue();

        await ctx.BankConnectionRepository.Received(1).UpdateAsync(
            Arg.Is<BankConnection>(c =>
                c.EncryptedSettings == "ENC_NEW_SETTINGS" &&
                c.ExternalAccountId == NewAccountId),
            Arg.Any<CancellationToken>());

        capturedEncrypt.Should().NotBeEmpty();
        var settingsObj = capturedEncrypt[^1];
        var accountIdProp = settingsObj.GetType().GetProperty("AkahuAccountId");
        accountIdProp.Should().NotBeNull("the encrypted settings object should expose AkahuAccountId");
        accountIdProp!.GetValue(settingsObj).Should().Be(NewAccountId);
    }

    private static TestContext CreateContext(Guid userId, int connectionId, int accountId, bool includeConnection = true)
    {
        var bankConnectionRepository = Substitute.For<IBankConnectionRepository>();
        var credentialRepository = Substitute.For<IAkahuUserCredentialRepository>();
        var akahuApiClient = Substitute.For<IAkahuApiClient>();
        var transactionRepository = Substitute.For<ITransactionRepository>();
        var encryptionService = Substitute.For<ISettingsEncryptionService>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var transaction = Substitute.For<IUnitOfWorkTransaction>();
        unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(transaction);
        var logger = Substitute.For<IApplicationLogger<MigrateAkahuConnectionCommandHandler>>();

        BankConnection? connection = null;
        if (includeConnection)
        {
            connection = new BankConnection
            {
                Id = connectionId,
                UserId = userId,
                AccountId = accountId,
                ProviderId = "akahu",
                ExternalAccountId = OldAccountId,
                ExternalAccountName = "Everyday (Classic)",
                EncryptedSettings = null,
                IsActive = true
            };
            bankConnectionRepository.GetByIdAsync(connectionId, Arg.Any<CancellationToken>())
                .Returns(connection);
        }
        else
        {
            bankConnectionRepository.GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns((BankConnection?)null);
        }

        var credential = new AkahuUserCredential
        {
            Id = 1,
            UserId = userId,
            EncryptedAppToken = "ENC_APP",
            EncryptedUserToken = "ENC_USER"
        };
        credentialRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(credential);

        encryptionService.DecryptSettings<string>("ENC_APP").Returns("app_token");
        encryptionService.DecryptSettings<string>("ENC_USER").Returns("user_token");
        encryptionService.EncryptSettings(Arg.Any<object>()).Returns("ENC_NEW_SETTINGS");

        return new TestContext(
            bankConnectionRepository,
            credentialRepository,
            akahuApiClient,
            transactionRepository,
            encryptionService,
            unitOfWork,
            transaction,
            logger,
            connection);
    }

    private sealed record TestContext(
        IBankConnectionRepository BankConnectionRepository,
        IAkahuUserCredentialRepository CredentialRepository,
        IAkahuApiClient AkahuApiClient,
        ITransactionRepository TransactionRepository,
        ISettingsEncryptionService EncryptionService,
        IUnitOfWork UnitOfWork,
        IUnitOfWorkTransaction Transaction,
        IApplicationLogger<MigrateAkahuConnectionCommandHandler> Logger,
        BankConnection? Connection)
    {
        public MigrateAkahuConnectionCommandHandler BuildHandler() => new(
            BankConnectionRepository,
            CredentialRepository,
            AkahuApiClient,
            TransactionRepository,
            EncryptionService,
            UnitOfWork,
            Logger);
    }
}
