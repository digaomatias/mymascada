using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categories.DTOs;

namespace MyMascada.Application.Features.Categories.Commands;

public class UpdateCategoryCommand : IRequest<CategoryDto>
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public int? ParentCategoryId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid UserId { get; set; }
}

public class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand, CategoryDto>
{
    private readonly ICategoryRepository _categoryRepository;

    public UpdateCategoryCommandHandler(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<CategoryDto> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(request.Id);
        
        if (category == null)
        {
            throw new ArgumentException("Category not found.");
        }
        
        // Check if user has permission to update this category
        if (category.IsSystemCategory || (category.UserId != request.UserId))
        {
            throw new UnauthorizedAccessException("You don't have permission to update this category.");
        }
        
        // Validate parent category if specified and different
        if (request.ParentCategoryId.HasValue && request.ParentCategoryId != category.ParentCategoryId)
        {
            // Check if parent exists
            var parentExists = await _categoryRepository.ExistsAsync(request.ParentCategoryId.Value, request.UserId);
            if (!parentExists)
            {
                throw new ArgumentException("Parent category not found or not accessible.");
            }
            
            // Prevent circular references
            if (request.ParentCategoryId == request.Id)
            {
                throw new ArgumentException("A category cannot be its own parent.");
            }
            
            // Check if the new parent would create a circular reference
            var wouldCreateCircularReference = await WouldCreateCircularReference(request.Id, request.ParentCategoryId.Value);
            if (wouldCreateCircularReference)
            {
                throw new ArgumentException("This parent assignment would create a circular reference.");
            }
        }

        // Update category properties
        category.Name = request.Name.Trim();
        category.Description = request.Description?.Trim();
        category.Color = request.Color?.Trim();
        category.Icon = request.Icon?.Trim();
        category.ParentCategoryId = request.ParentCategoryId;
        category.SortOrder = request.SortOrder;
        category.IsActive = request.IsActive;
        category.UpdatedAt = DateTime.UtcNow;

        await _categoryRepository.UpdateAsync(category);

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
            UpdatedAt = category.UpdatedAt
        };
    }

    private async Task<bool> WouldCreateCircularReference(int categoryId, int parentCategoryId)
    {
        var currentParent = await _categoryRepository.GetByIdAsync(parentCategoryId);
        
        while (currentParent != null)
        {
            if (currentParent.Id == categoryId)
            {
                return true; // Circular reference detected
            }
            
            if (currentParent.ParentCategoryId.HasValue)
            {
                currentParent = await _categoryRepository.GetByIdAsync(currentParent.ParentCategoryId.Value);
            }
            else
            {
                break;
            }
        }
        
        return false;
    }
}