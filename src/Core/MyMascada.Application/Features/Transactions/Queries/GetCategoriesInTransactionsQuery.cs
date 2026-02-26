using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Transactions.Queries;

public class GetCategoriesInTransactionsQuery : IRequest<IEnumerable<CategoryWithCountDto>>
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

public class CategoryWithCountDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? FullPath { get; set; }
    public int TransactionCount { get; set; }
    public int? ParentId { get; set; }
}

public class GetCategoriesInTransactionsQueryHandler : IRequestHandler<GetCategoriesInTransactionsQuery, IEnumerable<CategoryWithCountDto>>
{
    private readonly ICategoryRepository _categoryRepository;

    public GetCategoriesInTransactionsQueryHandler(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<IEnumerable<CategoryWithCountDto>> Handle(GetCategoriesInTransactionsQuery request, CancellationToken cancellationToken)
    {
        var categoriesWithCounts = await _categoryRepository.GetCategoriesWithTransactionCountsAsync(
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

        return categoriesWithCounts.Select(c => new CategoryWithCountDto
        {
            Id = c.Id,
            Name = c.Name,
            FullPath = c.FullPath,
            TransactionCount = c.TransactionCount,
            ParentId = c.ParentCategoryId
        });
    }
}