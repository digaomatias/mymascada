using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Reconciliation.DTOs;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Reconciliation.Queries;

public record GetReconciliationsQuery : IRequest<ReconciliationListResponse>
{
    public Guid UserId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public int? AccountId { get; init; }
    public ReconciliationStatus? Status { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string SortBy { get; init; } = "ReconciliationDate";
    public string SortDirection { get; init; } = "desc";
}

public class GetReconciliationsQueryHandler : IRequestHandler<GetReconciliationsQuery, ReconciliationListResponse>
{
    private readonly IReconciliationRepository _reconciliationRepository;
    private readonly IAccountRepository _accountRepository;

    public GetReconciliationsQueryHandler(
        IReconciliationRepository reconciliationRepository,
        IAccountRepository accountRepository)
    {
        _reconciliationRepository = reconciliationRepository;
        _accountRepository = accountRepository;
    }

    public async Task<ReconciliationListResponse> Handle(GetReconciliationsQuery request, CancellationToken cancellationToken)
    {
        var (reconciliations, totalCount) = await _reconciliationRepository.GetFilteredAsync(
            request.UserId,
            request.Page,
            request.PageSize,
            request.AccountId,
            request.Status,
            request.StartDate,
            request.EndDate,
            request.SortBy,
            request.SortDirection);

        // Get account names for the reconciliations
        var accountIds = reconciliations.Select(r => r.AccountId).Distinct().ToList();
        var accounts = await _accountRepository.GetByUserIdAsync(request.UserId);
        var accountLookup = accounts.ToDictionary(a => a.Id, a => a.Name);

        var reconciliationDtos = reconciliations.Select(r => new ReconciliationSummaryDto
        {
            Id = r.Id,
            AccountId = r.AccountId,
            AccountName = accountLookup.GetValueOrDefault(r.AccountId, "Unknown Account"),
            ReconciliationDate = r.ReconciliationDate,
            StatementEndDate = r.StatementEndDate,
            StatementEndBalance = r.StatementEndBalance,
            Status = r.Status,
            BalanceDifference = r.BalanceDifference,
            IsBalanced = r.IsBalanced,
            MatchedPercentage = r.MatchedPercentage
        }).ToList();

        var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

        return new ReconciliationListResponse
        {
            Reconciliations = reconciliationDtos,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = totalPages,
            HasNextPage = request.Page < totalPages,
            HasPreviousPage = request.Page > 1
        };
    }
}