using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.DTOs;

namespace MyMascada.Application.Features.BankConnections.Queries;

/// <summary>
/// Query to get detailed information about a single bank connection.
/// </summary>
public record GetBankConnectionQuery(
    Guid UserId,
    int BankConnectionId
) : IRequest<BankConnectionDetailDto?>;

/// <summary>
/// Handler for getting single bank connection details.
/// </summary>
public class GetBankConnectionQueryHandler : IRequestHandler<GetBankConnectionQuery, BankConnectionDetailDto?>
{
    private const int RecentSyncLogsLimit = 10;

    private readonly IBankConnectionRepository _bankConnectionRepository;
    private readonly IBankSyncLogRepository _syncLogRepository;
    private readonly IBankProviderFactory _providerFactory;
    private readonly IApplicationLogger<GetBankConnectionQueryHandler> _logger;

    public GetBankConnectionQueryHandler(
        IBankConnectionRepository bankConnectionRepository,
        IBankSyncLogRepository syncLogRepository,
        IBankProviderFactory providerFactory,
        IApplicationLogger<GetBankConnectionQueryHandler> logger)
    {
        _bankConnectionRepository = bankConnectionRepository ?? throw new ArgumentNullException(nameof(bankConnectionRepository));
        _syncLogRepository = syncLogRepository ?? throw new ArgumentNullException(nameof(syncLogRepository));
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BankConnectionDetailDto?> Handle(GetBankConnectionQuery request, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Fetching bank connection {ConnectionId} for user {UserId}",
            request.BankConnectionId, request.UserId);

        // 1. Get the connection
        var connection = await _bankConnectionRepository.GetByIdAsync(request.BankConnectionId, cancellationToken);
        if (connection == null)
        {
            _logger.LogDebug(
                "Bank connection {ConnectionId} not found",
                request.BankConnectionId);
            return null;
        }

        // 2. Verify ownership
        if (connection.UserId != request.UserId)
        {
            _logger.LogWarning(
                "User {UserId} does not own bank connection {ConnectionId} (owned by {OwnerId})",
                request.UserId, request.BankConnectionId, connection.UserId);
            return null;
        }

        // 3. Get recent sync logs
        var syncLogs = await _syncLogRepository.GetByBankConnectionIdAsync(
            request.BankConnectionId, RecentSyncLogsLimit, cancellationToken);

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

        // 4. Get provider display name
        var provider = _providerFactory.GetProviderOrDefault(connection.ProviderId);

        return new BankConnectionDetailDto
        {
            Id = connection.Id,
            AccountId = connection.AccountId,
            AccountName = connection.Account?.Name ?? "Unknown Account",
            ProviderId = connection.ProviderId,
            ProviderName = provider?.DisplayName ?? connection.ProviderId,
            ExternalAccountId = connection.ExternalAccountId,
            ExternalAccountName = connection.ExternalAccountName,
            IsActive = connection.IsActive,
            LastSyncAt = connection.LastSyncAt,
            LastSyncError = connection.LastSyncError,
            CreatedAt = connection.CreatedAt,
            RecentSyncLogs = syncLogDtos
        };
    }
}
