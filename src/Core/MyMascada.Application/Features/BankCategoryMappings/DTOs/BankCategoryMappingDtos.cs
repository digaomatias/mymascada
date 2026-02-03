namespace MyMascada.Application.Features.BankCategoryMappings.DTOs;

/// <summary>
/// DTO for displaying bank category mapping information.
/// </summary>
public record BankCategoryMappingDto
{
    public int Id { get; init; }
    public string BankCategoryName { get; init; } = string.Empty;
    public string ProviderId { get; init; } = string.Empty;
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public string? CategoryFullPath { get; init; }
    public decimal ConfidenceScore { get; init; }
    public decimal EffectiveConfidence { get; init; }
    public string Source { get; init; } = string.Empty;
    public int ApplicationCount { get; init; }
    public int OverrideCount { get; init; }
    public bool IsActive { get; init; }
    public bool IsExcluded { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// DTO for creating a new bank category mapping.
/// </summary>
public record CreateBankCategoryMappingDto
{
    public string BankCategoryName { get; init; } = string.Empty;
    public string ProviderId { get; init; } = "akahu";
    public int CategoryId { get; init; }
}

/// <summary>
/// DTO for updating an existing bank category mapping.
/// </summary>
public record UpdateBankCategoryMappingDto
{
    public int CategoryId { get; init; }
}

/// <summary>
/// Response DTO for listing bank category mappings.
/// </summary>
public record BankCategoryMappingsListDto
{
    public IEnumerable<BankCategoryMappingDto> Mappings { get; init; } = Array.Empty<BankCategoryMappingDto>();
    public int TotalCount { get; init; }
    public MappingStatisticsDto Statistics { get; init; } = new();
}

/// <summary>
/// Statistics about bank category mappings.
/// </summary>
public record MappingStatisticsDto
{
    public int TotalMappings { get; init; }
    public int AICreatedMappings { get; init; }
    public int UserCreatedMappings { get; init; }
    public int LearnedMappings { get; init; }
    public int HighConfidenceCount { get; init; }
    public int LowConfidenceCount { get; init; }
    public int TotalApplications { get; init; }
    public int TotalOverrides { get; init; }
}

/// <summary>
/// Request DTO for setting the exclusion status of a bank category mapping.
/// </summary>
public record SetExclusionRequestDto
{
    public bool IsExcluded { get; init; }
}
