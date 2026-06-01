using Hangfire;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyMascada.Application.BackgroundJobs;
using MyMascada.Application.Features.BankConnections.Commands;

namespace MyMascada.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire-backed implementation of <see cref="IAkahuMigrationJobService"/>.
/// Decouples the webhook/OAuth request lifetime from the migration's network and DB work.
/// </summary>
public class AkahuMigrationJobService : IAkahuMigrationJobService
{
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<AkahuMigrationJobService> _logger;

    public AkahuMigrationJobService(
        IBackgroundJobClient backgroundJobClient,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<AkahuMigrationJobService> logger)
    {
        _backgroundJobClient = backgroundJobClient ?? throw new ArgumentNullException(nameof(backgroundJobClient));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string EnqueueMigration(Guid userId, int bankConnectionId)
    {
        var jobId = _backgroundJobClient.Enqueue<IAkahuMigrationJobService>(
            s => s.ProcessMigrationAsync(userId, bankConnectionId));

        _logger.LogInformation(
            "Enqueued Akahu migration job {JobId} for connection {ConnectionId}",
            jobId, bankConnectionId);

        return jobId;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    public async Task ProcessMigrationAsync(Guid userId, int bankConnectionId)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        try
        {
            var result = await mediator.Send(new MigrateAkahuConnectionCommand(userId, bankConnectionId));
            _logger.LogInformation(
                "Akahu migration job complete for connection {ConnectionId}: success={Success}, txRemapped={TxRemapped}",
                bankConnectionId, result.Success, result.TransactionsRemapped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Akahu migration job failed for connection {ConnectionId}", bankConnectionId);
            throw; // Let Hangfire's AutomaticRetry handle this.
        }
    }
}
