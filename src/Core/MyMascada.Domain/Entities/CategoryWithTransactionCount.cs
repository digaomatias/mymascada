using System.ComponentModel.DataAnnotations;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Domain model representing a category with its associated transaction count.
/// This is used for reporting and filtering scenarios where we need category information
/// along with transaction statistics for specific filter criteria.
/// </summary>
public class CategoryWithTransactionCount
{
    /// <summary>
    /// Category identifier
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Name of the category (e.g., "Groceries", "Gas", "Entertainment")
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of what this category includes
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Hex color code for UI representation (e.g., "#FF5733")
    /// </summary>
    [MaxLength(7)]
    public string? Color { get; set; }

    /// <summary>
    /// Icon name or Unicode emoji for visual representation
    /// </summary>
    [MaxLength(50)]
    public string? Icon { get; set; }

    /// <summary>
    /// Whether this is a built-in system category or user-created
    /// </summary>
    public bool IsSystemCategory { get; set; } = false;

    /// <summary>
    /// Whether this category is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Sort order for displaying categories
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// User ID who owns this category (null for system categories)
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Parent category ID for creating category hierarchies
    /// </summary>
    public int? ParentCategoryId { get; set; }

    /// <summary>
    /// Parent category name for display purposes
    /// </summary>
    public string? ParentCategoryName { get; set; }

    /// <summary>
    /// Full hierarchical path of the category (e.g., "Food > Restaurants > Fast Food")
    /// </summary>
    public string FullPath { get; set; } = string.Empty;

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Last update timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Number of transactions in this category for the applied filters
    /// </summary>
    public int TransactionCount { get; set; }

    /// <summary>
    /// Total amount (sum) of transactions in this category for the applied filters
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Display name combining category name and transaction count
    /// (e.g., "Food & Dining (12)")
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Creates a CategoryWithTransactionCount from a Category entity and transaction count
    /// </summary>
    /// <param name="category">The source category</param>
    /// <param name="transactionCount">Number of transactions for this category</param>
    /// <param name="parentCategoryName">Name of the parent category (if any)</param>
    /// <param name="fullPath">Full hierarchical path</param>
    /// <returns>CategoryWithTransactionCount instance</returns>
    public static CategoryWithTransactionCount FromCategory(
        Category category, 
        int transactionCount, 
        string? parentCategoryName = null, 
        string? fullPath = null)
    {
        var result = new CategoryWithTransactionCount
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            Color = category.Color,
            Icon = category.Icon,
            IsSystemCategory = category.IsSystemCategory,
            IsActive = category.IsActive,
            SortOrder = category.SortOrder,
            UserId = category.UserId,
            ParentCategoryId = category.ParentCategoryId,
            ParentCategoryName = parentCategoryName,
            FullPath = fullPath ?? category.GetFullPath(" → "),
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt,
            TransactionCount = transactionCount
        };

        result.DisplayName = $"{result.FullPath} ({transactionCount})";
        return result;
    }
}