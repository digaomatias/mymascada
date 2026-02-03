using MyMascada.Domain.Entities;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Service for managing mappings between bank-provided categories (e.g., from Akahu)
/// and user's MyMascada categories. Handles AI-assisted mapping creation and lookup.
/// </summary>
public interface IBankCategoryMappingService
{
    /// <summary>
    /// Gets an existing mapping for a specific bank category.
    /// </summary>
    Task<BankCategoryMapping?> GetMappingAsync(
        string bankCategoryName,
        string providerId,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all mappings for a user, optionally filtered by provider.
    /// </summary>
    Task<IEnumerable<BankCategoryMapping>> GetUserMappingsAsync(
        Guid userId,
        string? providerId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Resolves categories for a batch of bank category names.
    /// Returns existing mappings only - does not create new ones.
    /// </summary>
    /// <returns>Dictionary mapping bank category name to resolved mapping (null if no mapping exists)</returns>
    Task<Dictionary<string, BankCategoryMappingResult?>> ResolveCategoriesAsync(
        IEnumerable<string> bankCategories,
        string providerId,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Resolves categories and creates AI mappings for any unmapped categories.
    /// This is the main method to use during transaction import.
    /// </summary>
    /// <returns>Dictionary mapping bank category name to resolved mapping</returns>
    Task<Dictionary<string, BankCategoryMappingResult>> ResolveAndCreateMappingsAsync(
        IEnumerable<string> bankCategories,
        string providerId,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Creates mappings using AI for unmapped bank categories.
    /// If AI suggests creating new categories, they will be created.
    /// </summary>
    Task<BankCategoryMappingResponse> CreateAIMappingsAsync(
        IEnumerable<string> unmappedCategories,
        string providerId,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Records successful application of a mapping to increment usage count.
    /// </summary>
    Task RecordMappingApplicationAsync(int mappingId, CancellationToken ct = default);

    /// <summary>
    /// Records that a user overrode a mapping with a different category.
    /// Updates override count and optionally creates/updates the mapping.
    /// </summary>
    Task RecordMappingOverrideAsync(
        int mappingId,
        int newCategoryId,
        bool createNewMapping,
        CancellationToken ct = default);

    /// <summary>
    /// Creates or updates a user-defined mapping.
    /// </summary>
    Task<BankCategoryMapping> UpsertMappingAsync(
        string bankCategoryName,
        string providerId,
        int categoryId,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a mapping (soft delete).
    /// </summary>
    Task<bool> DeleteMappingAsync(int mappingId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Gets a mapping by ID.
    /// </summary>
    Task<BankCategoryMapping?> GetByIdAsync(int mappingId, Guid userId, CancellationToken ct = default);
}

/// <summary>
/// Result of resolving a bank category to a MyMascada category.
/// </summary>
public class BankCategoryMappingResult
{
    /// <summary>
    /// The mapping entity (if it exists).
    /// </summary>
    public BankCategoryMapping? Mapping { get; set; }

    /// <summary>
    /// The resolved category ID.
    /// </summary>
    public int CategoryId { get; set; }

    /// <summary>
    /// The resolved category name for reference.
    /// </summary>
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>
    /// Confidence score for this mapping.
    /// </summary>
    public decimal ConfidenceScore { get; set; }

    /// <summary>
    /// Whether this mapping should auto-categorize (confidence >= 0.9).
    /// </summary>
    public bool ShouldAutoApply => ConfidenceScore >= 0.9m;

    /// <summary>
    /// Whether this mapping was newly created by AI.
    /// </summary>
    public bool WasCreatedByAI { get; set; }

    /// <summary>
    /// Whether a new category was created as part of this mapping.
    /// </summary>
    public bool NewCategoryCreated { get; set; }

    /// <summary>
    /// Whether this was an exact match between bank category name and user category name.
    /// Exact matches don't require a stored mapping - they match directly.
    /// </summary>
    public bool WasExactMatch { get; set; }

    /// <summary>
    /// Whether this bank category is excluded from automatic categorization.
    /// When true, transactions with this category should be passed to the next handler.
    /// </summary>
    public bool IsExcluded { get; set; }
}

/// <summary>
/// Response from AI-assisted mapping creation.
/// </summary>
public class BankCategoryMappingResponse
{
    public bool Success { get; set; }
    public List<BankCategoryMappingResult> Mappings { get; set; } = new();
    public BankCategoryMappingSummary Summary { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Summary of a batch mapping operation.
/// </summary>
public class BankCategoryMappingSummary
{
    public int TotalRequested { get; set; }
    public int MappedToExisting { get; set; }
    public int NewCategoriesCreated { get; set; }
    public int Failed { get; set; }
    public int ProcessingTimeMs { get; set; }
}

/// <summary>
/// AI response for mapping a single bank category.
/// </summary>
public class AIBankCategoryMappingResult
{
    /// <summary>
    /// The bank category name being mapped.
    /// </summary>
    public string BankCategory { get; set; } = string.Empty;

    /// <summary>
    /// Action to take: "MAP" to map to existing category, "CREATE_NEW" to create new category.
    /// </summary>
    public string Action { get; set; } = "MAP";

    /// <summary>
    /// Mapped category ID (for Action = "MAP").
    /// </summary>
    public int? MappedCategoryId { get; set; }

    /// <summary>
    /// Suggested name for new category (for Action = "CREATE_NEW").
    /// </summary>
    public string? SuggestedName { get; set; }

    /// <summary>
    /// Suggested parent category ID for new category (for Action = "CREATE_NEW").
    /// </summary>
    public int? SuggestedParentId { get; set; }

    /// <summary>
    /// Confidence score (0.0 to 1.0).
    /// </summary>
    public decimal Confidence { get; set; }

    /// <summary>
    /// AI's reasoning for this mapping decision.
    /// </summary>
    public string Reasoning { get; set; } = string.Empty;
}

/// <summary>
/// Response from AI for batch mapping request.
/// </summary>
public class AIBankCategoryMappingResponse
{
    public bool Success { get; set; }
    public List<AIBankCategoryMappingResult> Mappings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}
