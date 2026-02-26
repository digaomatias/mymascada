using MyMascada.Domain.Common;
using System.ComponentModel.DataAnnotations;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Represents a transaction category for organizing and tracking spending patterns.
/// Supports hierarchical structure with parent/child relationships.
/// </summary>
public class Category : BaseEntity
{
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
    /// Stable identifier for seeded categories, enabling matching regardless of display language or user renames.
    /// Null for user-created categories. Examples: "food_dining", "transportation", "income".
    /// </summary>
    [MaxLength(100)]
    public string? CanonicalKey { get; set; }

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

    // Hierarchical structure
    /// <summary>
    /// Parent category ID for creating category hierarchies
    /// </summary>
    public int? ParentCategoryId { get; set; }

    // Navigation properties
    /// <summary>
    /// Parent category for hierarchical organization
    /// </summary>
    public Category? ParentCategory { get; set; }

    /// <summary>
    /// Child categories under this category
    /// </summary>
    public ICollection<Category> SubCategories { get; set; } = new List<Category>();

    /// <summary>
    /// Transactions assigned to this category
    /// </summary>
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    /// <summary>
    /// Categorization rules that automatically assign this category
    /// </summary>
    public ICollection<CategorizationRule> CategorizationRules { get; set; } = new List<CategorizationRule>();

    /// <summary>
    /// Gets the full category path (e.g., "Food > Restaurants > Fast Food")
    /// </summary>
    public string GetFullPath(string separator = " > ")
    {
        var path = new List<string>();
        var current = this;
        
        while (current != null)
        {
            path.Insert(0, current.Name);
            current = current.ParentCategory;
        }
        
        return string.Join(separator, path);
    }

    /// <summary>
    /// Checks if this category is a descendant of the specified category
    /// </summary>
    public bool IsDescendantOf(Category category)
    {
        var current = ParentCategory;
        while (current != null)
        {
            if (current.Id == category.Id)
                return true;
            current = current.ParentCategory;
        }
        return false;
    }

    /// <summary>
    /// Gets all descendant categories (recursive)
    /// </summary>
    public IEnumerable<Category> GetAllDescendants()
    {
        var descendants = new List<Category>();
        foreach (var subCategory in SubCategories.Where(c => !c.IsDeleted))
        {
            descendants.Add(subCategory);
            descendants.AddRange(subCategory.GetAllDescendants());
        }
        return descendants;
    }
}