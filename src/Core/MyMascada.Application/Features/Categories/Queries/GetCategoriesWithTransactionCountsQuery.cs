using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Categories.Queries;

public class GetCategoriesWithTransactionCountsQuery : IRequest<IEnumerable<CategoryWithTransactionCount>>
{
    public Guid UserId { get; set; }
    public int? AccountId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public TransactionStatus? Status { get; set; }
    public string? SearchTerm { get; set; }
    public bool? IsReviewed { get; set; }
    public bool? IsExcluded { get; set; }
    public bool? IncludeTransfers { get; set; }
    public bool? OnlyTransfers { get; set; }
    public Guid? TransferId { get; set; }
}

public class GetCategoriesWithTransactionCountsQueryHandler : IRequestHandler<GetCategoriesWithTransactionCountsQuery, IEnumerable<CategoryWithTransactionCount>>
{
    private readonly ICategoryRepository _categoryRepository;

    public GetCategoriesWithTransactionCountsQueryHandler(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<IEnumerable<CategoryWithTransactionCount>> Handle(GetCategoriesWithTransactionCountsQuery request, CancellationToken cancellationToken)
    {
        return await _categoryRepository.GetCategoriesWithTransactionCountsAsync(
            request.UserId,
            request.AccountId,
            request.StartDate,
            request.EndDate,
            request.MinAmount,
            request.MaxAmount,
            request.Status,
            request.SearchTerm,
            request.IsReviewed,
            request.IsExcluded,
            request.IncludeTransfers,
            request.OnlyTransfers,
            request.TransferId);
    }
}
