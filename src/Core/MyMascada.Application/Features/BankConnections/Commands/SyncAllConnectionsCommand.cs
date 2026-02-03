using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.DTOs;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.BankConnections.Commands;

/// <summary>
/// Command to sync all active bank connections for a user.
/// </summary>
public record SyncAllConnectionsCommand(
    Guid UserId
) : IRequest<IEnumerable<BankSyncResultDto>>;

/// <summary>
/// Handler for syncing all bank connections for a user.
/// </summary>
public class SyncAllConnectionsCommandHandler : IRequestHandler<SyncAllConnectionsCommand, IEnumerable<BankSyncResultDto>>
{
    private readonly IBankSyncService _bankSyncService;
    private readonly IApplicationLogger<SyncAllConnectionsCommandHandler> _logger;

    public SyncAllConnectionsCommandHandler(
        IBankSyncService bankSyncService,
        IApplicationLogger<SyncAllConnectionsCommandHandler> logger)
    {
        _bankSyncService = bankSyncService ?? throw new ArgumentNullException(nameof(bankSyncService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<BankSyncResultDto>> Handle(SyncAllConnectionsCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Syncing all active bank connections for user {UserId}",
            request.UserId);

        var results = await _bankSyncService.SyncAllConnectionsAsync(
            request.UserId,
            BankSyncType.Manual,
            cancellationToken);

        var resultDtos = results.Select(r => new BankSyncResultDto
        {
            BankConnectionId = r.BankConnectionId,
            IsSuccess = r.IsSuccess,
            ErrorMessage = r.ErrorMessage,
            TransactionsImported = r.TransactionsImported,
            TransactionsSkipped = r.TransactionsSkipped
        }).ToList();

        var successCount = resultDtos.Count(r => r.IsSuccess);
        var totalImported = resultDtos.Sum(r => r.TransactionsImported);
        var totalSkipped = resultDtos.Sum(r => r.TransactionsSkipped);

        _logger.LogInformation(
            "Completed sync all for user {UserId}: {SuccessCount}/{TotalCount} successful, {TotalImported} imported, {TotalSkipped} skipped",
            request.UserId, successCount, resultDtos.Count, totalImported, totalSkipped);

        return resultDtos;
    }
}
