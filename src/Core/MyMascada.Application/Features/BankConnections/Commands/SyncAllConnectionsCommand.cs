using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.DTOs;

namespace MyMascada.Application.Features.BankConnections.Commands;

/// <summary>
/// Command to sync all active bank connections for a user.
/// </summary>
public record SyncAllConnectionsCommand(
    Guid UserId
) : IRequest<BankSyncJobAcceptedDto>;

/// <summary>
/// Handler for enqueuing a sync-all job for a user's active bank connections.
/// </summary>
public class SyncAllConnectionsCommandHandler : IRequestHandler<SyncAllConnectionsCommand, BankSyncJobAcceptedDto>
{
    private readonly IBankConnectionRepository _bankConnectionRepository;
    private readonly IBankSyncJobService _bankSyncJobService;
    private readonly IApplicationLogger<SyncAllConnectionsCommandHandler> _logger;

    public SyncAllConnectionsCommandHandler(
        IBankConnectionRepository bankConnectionRepository,
        IBankSyncJobService bankSyncJobService,
        IApplicationLogger<SyncAllConnectionsCommandHandler> logger)
    {
        _bankConnectionRepository = bankConnectionRepository ?? throw new ArgumentNullException(nameof(bankConnectionRepository));
        _bankSyncJobService = bankSyncJobService ?? throw new ArgumentNullException(nameof(bankSyncJobService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BankSyncJobAcceptedDto> Handle(SyncAllConnectionsCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Queueing sync for all active bank connections for user {UserId}",
            request.UserId);

        var activeConnectionIds = (await _bankConnectionRepository.GetActiveByUserIdAsync(
                request.UserId,
                cancellationToken))
            .Select(connection => connection.Id)
            .Distinct()
            .ToArray();

        if (activeConnectionIds.Length == 0)
        {
            throw new InvalidOperationException("No active bank connections found to sync.");
        }

        var accepted = _bankSyncJobService.EnqueueAllConnectionsSync(
            request.UserId,
            activeConnectionIds);

        _logger.LogInformation(
            "Queued sync-all job {JobId} for user {UserId} covering {ConnectionCount} connections",
            accepted.JobId,
            request.UserId,
            activeConnectionIds.Length);

        return accepted;
    }
}
