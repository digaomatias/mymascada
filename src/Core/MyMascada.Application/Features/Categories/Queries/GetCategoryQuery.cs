using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categories.DTOs;

namespace MyMascada.Application.Features.Categories.Queries;

public class GetCategoryQuery : IRequest<CategoryDto?>
{
    public int CategoryId { get; set; }
    public Guid UserId { get; set; }
}

public class GetCategoryQueryHandler : IRequestHandler<GetCategoryQuery, CategoryDto?>
{
    private readonly ICategoryRepository _categoryRepository;

    public GetCategoryQueryHandler(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<CategoryDto?> Handle(GetCategoryQuery request, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(request.CategoryId);
        
        if (category == null)
            return null;
            
        // Check if user has access to this category (own category or system category)
        if (!category.IsSystemCategory && category.UserId != request.UserId)
            return null;

        // Get transaction statistics for this category
        var categoryStats = await _categoryRepository.GetCategoriesWithTransactionCountsAsync(request.UserId);
        var stats = categoryStats.FirstOrDefault(c => c.Id == category.Id);

        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            CanonicalKey = category.CanonicalKey,
            Description = category.Description,
            Color = category.Color,
            Icon = category.Icon,
            IsSystemCategory = category.IsSystemCategory,
            IsActive = category.IsActive,
            SortOrder = category.SortOrder,
            ParentCategoryId = category.ParentCategoryId,
            FullPath = category.GetFullPath(),
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt,
            TransactionCount = stats?.TransactionCount ?? 0,
            TotalAmount = stats?.TotalAmount ?? 0
        };
    }
}