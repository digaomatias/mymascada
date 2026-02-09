using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Events;
using MyMascada.Application.Features.BankConnections.DTOs;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.BankConnections.Commands;

/// <summary>
/// Command to trigger a manual sync for a bank connection.
/// </summary>
public record SyncBankConnectionCommand(
    Guid UserId,
    int BankConnectionId
) : IRequest<BankSyncResultDto>;

/// <summary>
/// Handler for syncing a single bank connection.
/// </summary>
public class SyncBankConnectionCommandHandler : IRequestHandler<SyncBankConnectionCommand, BankSyncResultDto>
{
    private readonly IBankConnectionRepository _bankConnectionRepository;
    private readonly IBankSyncService _bankSyncService;
    private readonly IMediator _mediator;
    private readonly IApplicationLogger<SyncBankConnectionCommandHandler> _logger;

    public SyncBankConnectionCommandHandler(
        IBankConnectionRepository bankConnectionRepository,
        IBankSyncService bankSyncService,
        IMediator mediator,
        IApplicationLogger<SyncBankConnectionCommandHandler> logger)
    {
        _bankConnectionRepository = bankConnectionRepository ?? throw new ArgumentNullException(nameof(bankConnectionRepository));
        _bankSyncService = bankSyncService ?? throw new ArgumentNullException(nameof(bankSyncService));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BankSyncResultDto> Handle(SyncBankConnectionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Triggering manual sync for bank connection {ConnectionId} by user {UserId}",
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
            return new BankSyncResultDto
            {
                BankConnectionId = request.BankConnectionId,
                IsSuccess = false,
                ErrorMessage = "Bank connection is not active. Please reconnect.",
                TransactionsImported = 0,
                TransactionsSkipped = 0
            };
        }

        // 3. Perform the sync
        var result = await _bankSyncService.SyncAccountAsync(
            request.BankConnectionId,
            BankSyncType.Manual,
            cancellationToken);

        _logger.LogInformation(
            "Completed sync for bank connection {ConnectionId}: Success={IsSuccess}, Imported={Imported}, Skipped={Skipped}",
            request.BankConnectionId, result.IsSuccess, result.TransactionsImported, result.TransactionsSkipped);

        // Publish TransactionsCreatedEvent to trigger description cleaning and categorization
        if (result.IsSuccess && result.ImportedTransactionIds.Any())
        {
            await _mediator.Publish(
                new TransactionsCreatedEvent(result.ImportedTransactionIds, request.UserId),
                cancellationToken);

            _logger.LogInformation(
                "Published TransactionsCreatedEvent for {TransactionCount} imported transactions from bank sync",
                result.ImportedTransactionIds.Count);
        }

        return new BankSyncResultDto
        {
            BankConnectionId = result.BankConnectionId,
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            TransactionsImported = result.TransactionsImported,
            TransactionsSkipped = result.TransactionsSkipped
        };
    }
}
