using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categories.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Categories.Queries;

/// <summary>
/// Query to get categories that exist in the current filtered transaction set
/// This supports dynamic category filtering in the UI
/// </summary>
public class GetFilteredCategoriesQuery : IRequest<IEnumerable<CategoryWithTransactionCountDto>>
{
    public Guid UserId { get; set; }
    public bool IncludeSystemCategories { get; set; } = true;
    public bool IncludeInactive { get; set; } = false;
    
    // Transaction filtering parameters to determine which categories to show
    public string? SearchTerm { get; set; }
    public int? AccountId { get; set; }
    public bool? IsReviewed { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public bool? OnlyTransfers { get; set; }
    public bool? IncludeTransfers { get; set; }
}

public class GetFilteredCategoriesQueryHandler : IRequestHandler<GetFilteredCategoriesQuery, IEnumerable<CategoryWithTransactionCountDto>>
{
    private readonly ICategoryRepository _categoryRepository;

    public GetFilteredCategoriesQueryHandler(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<IEnumerable<CategoryWithTransactionCountDto>> Handle(
        GetFilteredCategoriesQuery request, 
        CancellationToken cancellationToken)
    {
        // Get categories with transaction counts based on current filters
        var categoryStatsList = (await _categoryRepository.GetCategoriesWithTransactionCountsAsync(
            request.UserId,
            request.AccountId,
            request.StartDate != null ? DateTime.Parse(request.StartDate) : (DateTime?)null,
            request.EndDate != null ? DateTime.Parse(request.EndDate) : (DateTime?)null,
            null, // MinAmount
            null, // MaxAmount
            null, // Status
            request.SearchTerm,
            request.IsReviewed,
            null, // IsExcluded
            request.IncludeTransfers,
            request.OnlyTransfers,
            null)).ToList(); // TransferId

        var categoryStats = categoryStatsList.ToDictionary(c => c.Id, c => c.TransactionCount);
        
        // Get all available categories for the user
        var categories = new List<Category>();
        
        // Get user categories
        var userCategories = await _categoryRepository.GetByUserIdAsync(request.UserId);
        categories.AddRange(userCategories);
        
        // Get system categories if requested
        if (request.IncludeSystemCategories)
        {
            var systemCategories = await _categoryRepository.GetSystemCategoriesAsync();
            categories.AddRange(systemCategories);
        }
        
        // Filter inactive categories if needed
        if (!request.IncludeInactive)
        {
            categories = categories.Where(c => c.IsActive).ToList();
        }
        
        // Build category map for full path generation
        var categoryMap = categories.ToDictionary(c => c.Id, c => c);
        
        // Create DTOs with transaction counts
        var result = categories
            .Where(c => categoryStats.ContainsKey(c.Id)) // Only include categories that have transactions in the filtered set
            .Select(c => new CategoryWithTransactionCountDto
            {
                Id = c.Id,
                Name = c.Name,
                CanonicalKey = c.CanonicalKey,
                Description = c.Description,
                Color = c.Color,
                Icon = c.Icon,
                Type = c.Type,
                IsSystemCategory = c.IsSystemCategory,
                IsActive = c.IsActive,
                SortOrder = c.SortOrder,
                ParentCategoryId = c.ParentCategoryId,
                FullPath = BuildFullPath(c.Id, categoryMap, " → "),
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                TransactionCount = categoryStats[c.Id],
                DisplayName = $"{BuildFullPath(c.Id, categoryMap, " → ")} ({categoryStats[c.Id]})"
            })
            .OrderByDescending(c => c.TransactionCount) // Most used categories first
            .ThenBy(c => c.FullPath)
            .ToList();

        return result;
    }
    
    private static string BuildFullPath(int categoryId, Dictionary<int, Category> categoryMap, string separator)
    {
        var path = new List<string>();
        var currentId = categoryId;
        
        while (categoryMap.ContainsKey(currentId))
        {
            var current = categoryMap[currentId];
            path.Insert(0, current.Name);
            
            if (!current.ParentCategoryId.HasValue)
                break;
                
            currentId = current.ParentCategoryId.Value;
        }
        
        return string.Join(separator, path);
    }
}