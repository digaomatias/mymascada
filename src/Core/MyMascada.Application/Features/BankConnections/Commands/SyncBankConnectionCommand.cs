using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.DTOs;

namespace MyMascada.Application.Features.BankConnections.Commands;

/// <summary>
/// Command to trigger a manual sync for a bank connection.
/// </summary>
public record SyncBankConnectionCommand(
    Guid UserId,
    int BankConnectionId
) : IRequest<BankSyncJobAcceptedDto>;

/// <summary>
/// Handler for enqueuing a single bank connection sync.
/// </summary>
public class SyncBankConnectionCommandHandler : IRequestHandler<SyncBankConnectionCommand, BankSyncJobAcceptedDto>
{
    private readonly IBankConnectionRepository _bankConnectionRepository;
    private readonly IBankSyncJobService _bankSyncJobService;
    private readonly IApplicationLogger<SyncBankConnectionCommandHandler> _logger;

    public SyncBankConnectionCommandHandler(
        IBankConnectionRepository bankConnectionRepository,
        IBankSyncJobService bankSyncJobService,
        IApplicationLogger<SyncBankConnectionCommandHandler> logger)
    {
        _bankConnectionRepository = bankConnectionRepository ?? throw new ArgumentNullException(nameof(bankConnectionRepository));
        _bankSyncJobService = bankSyncJobService ?? throw new ArgumentNullException(nameof(bankSyncJobService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BankSyncJobAcceptedDto> Handle(SyncBankConnectionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Queueing manual sync for bank connection {ConnectionId} by user {UserId}",
            request.BankConnectionId, request.UserId);

        // 1. Verify the connection exists and belongs to the user
        var connection = await _bankConnectionRepository.GetByIdAsync(request.BankConnectionId, cancellationToken);
        if (connection == null)
        {
            _logger.LogWarning(
                "Bank connection {ConnectionId} not found",
                request.BankConnectionId);
            throw new ArgumentException($"Bank connection {request.BankConnectionId} not found");
        }

        if (connection.UserId != request.UserId)
        {
            _logger.LogWarning(
                "User {UserId} does not own bank connection {ConnectionId} (owned by {OwnerId})",
                request.UserId, request.BankConnectionId, connection.UserId);
            throw new UnauthorizedAccessException("You do not have access to this bank connection");
        }

        // 2. Check if the connection is active
        if (!connection.IsActive)
        {
            _logger.LogWarning(
                "Bank connection {ConnectionId} is not active",
                request.BankConnectionId);
            throw new InvalidOperationException("Bank connection is not active. Please reconnect.");
        }

        var accepted = _bankSyncJobService.EnqueueConnectionSync(
            request.UserId,
            request.BankConnectionId);

        _logger.LogInformation(
            "Queued sync job {JobId} for bank connection {ConnectionId}",
            accepted.JobId,
            request.BankConnectionId);

        return accepted;
    }
}
