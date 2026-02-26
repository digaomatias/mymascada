namespace MyMascada.Application.Features.Categories.DTOs;

public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CanonicalKey { get; set; }
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public bool IsSystemCategory { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public int? ParentCategoryId { get; set; }
    public string? ParentCategoryName { get; set; }
    public string FullPath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Statistics (optional)
    public int TransactionCount { get; set; }
    public decimal TotalAmount { get; set; }

    // Navigation for hierarchical display
    public List<CategoryDto> SubCategories { get; set; } = new();
}

public class CreateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public int? ParentCategoryId { get; set; }
    public int SortOrder { get; set; }
}

public class UpdateCategoryRequest
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public int? ParentCategoryId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CategoryWithTransactionCountDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CanonicalKey { get; set; }
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public bool IsSystemCategory { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public int? ParentCategoryId { get; set; }
    public string? ParentCategoryName { get; set; }
    public string FullPath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Transaction count for filtered results
    public int TransactionCount { get; set; }
    public decimal TotalAmount { get; set; }
    public string DisplayName { get; set; } = string.Empty; // e.g., "Food & Dining (12)"
}

public class CategoryStatisticsDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AverageAmount { get; set; }
    public DateTime? LastTransactionDate { get; set; }
    public DateTime? FirstTransactionDate { get; set; }
}