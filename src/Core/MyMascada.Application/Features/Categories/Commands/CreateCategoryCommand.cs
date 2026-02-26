using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categories.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Categories.Commands;

public class CreateCategoryCommand : IRequest<CategoryDto>
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public int? ParentCategoryId { get; set; }
    public int SortOrder { get; set; }
    public Guid UserId { get; set; }
}

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, CategoryDto>
{
    private readonly ICategoryRepository _categoryRepository;

    public CreateCategoryCommandHandler(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<CategoryDto> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        // Validate parent category if specified
        if (request.ParentCategoryId.HasValue)
        {
            var parentExists = await _categoryRepository.ExistsAsync(request.ParentCategoryId.Value, request.UserId);
            if (!parentExists)
            {
                throw new ArgumentException("Parent category not found or not accessible.");
            }
        }

        var category = new Category
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Color = request.Color?.Trim(),
            Icon = request.Icon?.Trim(),
            ParentCategoryId = request.ParentCategoryId,
            SortOrder = request.SortOrder,
            UserId = request.UserId,
            IsSystemCategory = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var createdCategory = await _categoryRepository.AddAsync(category);

        return new CategoryDto
        {
            Id = createdCategory.Id,
            Name = createdCategory.Name,
            CanonicalKey = createdCategory.CanonicalKey,
            Description = createdCategory.Description,
            Color = createdCategory.Color,
            Icon = createdCategory.Icon,
            IsSystemCategory = createdCategory.IsSystemCategory,
            IsActive = createdCategory.IsActive,
            SortOrder = createdCategory.SortOrder,
            ParentCategoryId = createdCategory.ParentCategoryId,
            FullPath = createdCategory.GetFullPath(),
            CreatedAt = createdCategory.CreatedAt,
            UpdatedAt = createdCategory.UpdatedAt
        };
    }
}