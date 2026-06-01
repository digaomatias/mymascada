using MyMascada.Application.BackgroundJobs;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.Commands;
using MyMascada.Application.Features.BankConnections.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Tests.Unit.Commands;

public class ProcessAkahuWebhookCommandHandlerTests
{
    [Fact]
    public async Task Handle_AccountMigrateEvent_EnqueuesJobInsteadOfRunningInline()
    {
        // Regression: previously the handler called _mediator.Send for MigrateAkahuConnectionCommand
        // and awaited it. That blocked the webhook response until Akahu's API calls and DB remap
        // completed, risking webhook timeouts. The handler must hand off to Hangfire.
        var bankConnectionRepository = Substitute.For<IBankConnectionRepository>();
        var bankSyncService = Substitute.For<IBankSyncService>();
        var credentialRepository = Substitute.For<IAkahuUserCredentialRepository>();
        var transactionRepository = Substitute.For<ITransactionRepository>();
        var subscriptionRepository = Substitute.For<IAkahuWebhookSubscriptionRepository>();
        var migrationJobService = Substitute.For<IAkahuMigrationJobService>();
        var logger = Substitute.For<IApplicationLogger<ProcessAkahuWebhookCommandHandler>>();

        var userId = Guid.NewGuid();
        var connection = new BankConnection
        {
            Id = 42,
            UserId = userId,
            ProviderId = "akahu",
            ExternalAccountId = "acc_OLD",
            IsActive = true
        };
        bankConnectionRepository.GetByExternalAccountIdAsync("acc_OLD", "akahu", Arg.Any<CancellationToken>())
            .Returns(connection);

        migrationJobService.EnqueueMigration(userId, 42).Returns("job-123");

        var handler = new ProcessAkahuWebhookCommandHandler(
            bankConnectionRepository,
            bankSyncService,
            credentialRepository,
            transactionRepository,
            subscriptionRepository,
            migrationJobService,
            logger);

        var payload = new AkahuWebhookPayload
        {
            WebhookType = AkahuWebhookTypes.Account,
            WebhookCode = AkahuWebhookCodes.Migrate,
            PreviousItemId = "acc_OLD",
            ItemId = "acc_NEW"
        };

        await handler.Handle(new ProcessAkahuWebhookCommand(payload), CancellationToken.None);

        migrationJobService.Received(1).EnqueueMigration(userId, 42);
    }
}
