using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categories.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Categories.Queries;

public class GetCategoriesQuery : IRequest<IEnumerable<CategoryDto>>
{
    public Guid UserId { get; set; }
    public bool IncludeSystemCategories { get; set; } = true;
    public bool IncludeInactive { get; set; } = false;
    public bool IncludeHierarchy { get; set; } = false;
}

public class GetCategoriesQueryHandler : IRequestHandler<GetCategoriesQuery, IEnumerable<CategoryDto>>
{
    private readonly ICategoryRepository _categoryRepository;

    public GetCategoriesQueryHandler(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<IEnumerable<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
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
        
        // Convert to DTOs and build full paths
        var categoryDtos = categories.Select(c => new CategoryDto
        {
            Id = c.Id,
            Name = c.Name,
            CanonicalKey = c.CanonicalKey,
            Description = c.Description,
            Color = c.Color,
            Icon = c.Icon,
            IsSystemCategory = c.IsSystemCategory,
            IsActive = c.IsActive,
            SortOrder = c.SortOrder,
            ParentCategoryId = c.ParentCategoryId,
            FullPath = "", // Will be set below
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        }).ToList();
        
        // Build full paths manually since navigation properties might not be loaded
        var categoryMap = categories.ToDictionary(c => c.Id, c => c);
        foreach (var dto in categoryDtos)
        {
            dto.FullPath = BuildFullPath(dto.Id, categoryMap, " -> ");
        }
        
        // Build hierarchy if requested
        if (request.IncludeHierarchy)
        {
            return BuildHierarchy(categoryDtos);
        }
        
        return categoryDtos.OrderBy(c => c.SortOrder).ThenBy(c => c.Name);
    }
    
    private static IEnumerable<CategoryDto> BuildHierarchy(List<CategoryDto> categories)
    {
        var categoryLookup = categories.ToDictionary(c => c.Id);
        var rootCategories = new List<CategoryDto>();
        
        foreach (var category in categories)
        {
            if (category.ParentCategoryId.HasValue && categoryLookup.ContainsKey(category.ParentCategoryId.Value))
            {
                var parent = categoryLookup[category.ParentCategoryId.Value];
                parent.SubCategories.Add(category);
            }
            else
            {
                rootCategories.Add(category);
            }
        }
        
        // Sort each level
        SortCategoriesRecursive(rootCategories);
        
        return rootCategories;
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
    
    private static void SortCategoriesRecursive(List<CategoryDto> categories)
    {
        categories.Sort((a, b) => 
        {
            var sortOrderComparison = a.SortOrder.CompareTo(b.SortOrder);
            return sortOrderComparison != 0 ? sortOrderComparison : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });
        
        foreach (var category in categories)
        {
            if (category.SubCategories.Any())
            {
                SortCategoriesRecursive(category.SubCategories);
            }
        }
    }
}