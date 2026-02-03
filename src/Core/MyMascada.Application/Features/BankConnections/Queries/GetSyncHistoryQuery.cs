using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.DTOs;

namespace MyMascada.Application.Features.BankConnections.Queries;

/// <summary>
/// Query to get sync history for a bank connection.
/// </summary>
public record GetSyncHistoryQuery(
    Guid UserId,
    int BankConnectionId,
    int Limit = 20
) : IRequest<IEnumerable<BankSyncLogDto>>;

/// <summary>
/// Handler for getting sync history.
/// </summary>
public class GetSyncHistoryQueryHandler : IRequestHandler<GetSyncHistoryQuery, IEnumerable<BankSyncLogDto>>
{
    private const int MaxLimit = 100;

    private readonly IBankConnectionRepository _bankConnectionRepository;
    private readonly IBankSyncLogRepository _syncLogRepository;
    private readonly IApplicationLogger<GetSyncHistoryQueryHandler> _logger;

    public GetSyncHistoryQueryHandler(
        IBankConnectionRepository bankConnectionRepository,
        IBankSyncLogRepository syncLogRepository,
        IApplicationLogger<GetSyncHistoryQueryHandler> logger)
    {
        _bankConnectionRepository = bankConnectionRepository ?? throw new ArgumentNullException(nameof(bankConnectionRepository));
        _syncLogRepository = syncLogRepository ?? throw new ArgumentNullException(nameof(syncLogRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<BankSyncLogDto>> Handle(GetSyncHistoryQuery request, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Fetching sync history for connection {ConnectionId}, user {UserId}, limit {Limit}",
            request.BankConnectionId, request.UserId, request.Limit);

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

        // 2. Apply limit constraints
        var effectiveLimit = Math.Min(Math.Max(1, request.Limit), MaxLimit);

        // 3. Get sync logs
        var syncLogs = await _syncLogRepository.GetByBankConnectionIdAsync(
            request.BankConnectionId, effectiveLimit, cancellationToken);

        // 4. Map to DTOs
        var syncLogDtos = syncLogs.Select(log => new BankSyncLogDto
        {
            Id = log.Id,
            SyncType = log.SyncType.ToString(),
            Status = log.Status.ToString(),
            StartedAt = log.StartedAt,
            CompletedAt = log.CompletedAt,
            TransactionsProcessed = log.TransactionsProcessed,
            TransactionsImported = log.TransactionsImported,
            TransactionsSkipped = log.TransactionsSkipped,
            ErrorMessage = log.ErrorMessage
        }).ToList();

        _logger.LogDebug(
            "Found {Count} sync logs for connection {ConnectionId}",
            syncLogDtos.Count, request.BankConnectionId);

        return syncLogDtos;
    }
}
