using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Reconciliation.DTOs;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Reconciliation.Queries;

public record GetReconciliationQuery : IRequest<ReconciliationDto?>
{
    public int ReconciliationId { get; init; }
    public Guid UserId { get; init; }
}

public class GetReconciliationQueryHandler : IRequestHandler<GetReconciliationQuery, ReconciliationDto?>
{
    private readonly IReconciliationRepository _reconciliationRepository;
    private readonly IReconciliationItemRepository _reconciliationItemRepository;
    private readonly IAccountRepository _accountRepository;

    public GetReconciliationQueryHandler(
        IReconciliationRepository reconciliationRepository,
        IReconciliationItemRepository reconciliationItemRepository,
        IAccountRepository accountRepository)
    {
        _reconciliationRepository = reconciliationRepository;
        _reconciliationItemRepository = reconciliationItemRepository;
        _accountRepository = accountRepository;
    }

    public async Task<ReconciliationDto?> Handle(GetReconciliationQuery request, CancellationToken cancellationToken)
    {
        var reconciliation = await _reconciliationRepository.GetByIdAsync(request.ReconciliationId, request.UserId);
        if (reconciliation == null)
            return null;

        var account = await _accountRepository.GetByIdAsync(reconciliation.AccountId, request.UserId);
        if (account == null)
            return null;

        return new ReconciliationDto
        {
            Id = reconciliation.Id,
            AccountId = reconciliation.AccountId,
            AccountName = account.Name,
            ReconciliationDate = reconciliation.ReconciliationDate,
            StatementEndDate = reconciliation.StatementEndDate,
            StatementEndBalance = reconciliation.StatementEndBalance,
            CalculatedBalance = reconciliation.CalculatedBalance,
            Status = reconciliation.Status,
            CreatedByUserId = reconciliation.CreatedByUserId,
            CompletedAt = reconciliation.CompletedAt,
            Notes = reconciliation.Notes,
            BalanceDifference = reconciliation.BalanceDifference,
            IsBalanced = reconciliation.IsBalanced,
            TotalItemsCount = reconciliation.TotalItemsCount,
            MatchedItemsCount = reconciliation.MatchedItemsCount,
            MatchedPercentage = reconciliation.MatchedPercentage,
            CreatedAt = reconciliation.CreatedAt,
            UpdatedAt = reconciliation.UpdatedAt
        };
    }
}