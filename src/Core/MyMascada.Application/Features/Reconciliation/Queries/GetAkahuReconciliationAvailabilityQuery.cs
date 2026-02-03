using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.Queries;
using MyMascada.Application.Features.Reconciliation.DTOs;

namespace MyMascada.Application.Features.Reconciliation.Queries;

/// <summary>
/// Query to check if Akahu reconciliation is available for a specific account
/// </summary>
public record GetAkahuReconciliationAvailabilityQuery : IRequest<AkahuAvailabilityResponse>
{
    public int AccountId { get; init; }
    public Guid UserId { get; init; }
}

/// <summary>
/// Handler for checking Akahu reconciliation availability
/// </summary>
public class GetAkahuReconciliationAvailabilityQueryHandler
    : IRequestHandler<GetAkahuReconciliationAvailabilityQuery, AkahuAvailabilityResponse>
{
    private readonly IAccountRepository _accountRepository;
    private readonly IBankConnectionRepository _bankConnectionRepository;
    private readonly IMediator _mediator;
    private readonly IApplicationLogger<GetAkahuReconciliationAvailabilityQueryHandler> _logger;

    public GetAkahuReconciliationAvailabilityQueryHandler(
        IAccountRepository accountRepository,
        IBankConnectionRepository bankConnectionRepository,
        IMediator mediator,
        IApplicationLogger<GetAkahuReconciliationAvailabilityQueryHandler> logger)
    {
        _accountRepository = accountRepository;
        _bankConnectionRepository = bankConnectionRepository;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<AkahuAvailabilityResponse> Handle(
        GetAkahuReconciliationAvailabilityQuery request,
        CancellationToken cancellationToken)
    {
        // Validate account exists and belongs to user
        var account = await _accountRepository.GetByIdAsync(request.AccountId, request.UserId);
        if (account == null)
        {
            _logger.LogWarning(
                "Account {AccountId} not found or does not belong to user {UserId}",
                request.AccountId,
                request.UserId);

            return new AkahuAvailabilityResponse
            {
                IsAvailable = false,
                UnavailableReason = "Account not found"
            };
        }

        // Check if account has a bank connection
        var bankConnection = await _bankConnectionRepository.GetByAccountIdAsync(
            request.AccountId,
            cancellationToken);

        if (bankConnection == null)
        {
            return new AkahuAvailabilityResponse
            {
                IsAvailable = false,
                UnavailableReason = "No bank connection found. Connect this account to Akahu first."
            };
        }

        // Check if it's an Akahu connection
        if (!string.Equals(bankConnection.ProviderId, "akahu", StringComparison.OrdinalIgnoreCase))
        {
            return new AkahuAvailabilityResponse
            {
                IsAvailable = false,
                UnavailableReason = $"Bank connection uses provider '{bankConnection.ProviderId}', not Akahu."
            };
        }

        // Check if the connection has an external account ID
        if (string.IsNullOrEmpty(bankConnection.ExternalAccountId))
        {
            return new AkahuAvailabilityResponse
            {
                IsAvailable = false,
                UnavailableReason = "Bank connection is incomplete. Please reconnect your account."
            };
        }

        // Check if user has valid Akahu credentials
        var credentials = await _mediator.Send(
            new GetAkahuCredentialsQuery(request.UserId),
            cancellationToken);

        if (credentials == null)
        {
            return new AkahuAvailabilityResponse
            {
                IsAvailable = false,
                ExternalAccountId = bankConnection.ExternalAccountId,
                UnavailableReason = "Bank connection needs re-authentication. Please reconnect your account."
            };
        }

        // All checks passed - Akahu reconciliation is available
        _logger.LogDebug(
            "Akahu reconciliation available for account {AccountId} with external ID {ExternalAccountId}",
            request.AccountId,
            bankConnection.ExternalAccountId);

        return new AkahuAvailabilityResponse
        {
            IsAvailable = true,
            ExternalAccountId = bankConnection.ExternalAccountId
        };
    }
}
