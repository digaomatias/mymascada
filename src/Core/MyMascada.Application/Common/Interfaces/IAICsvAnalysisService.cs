using MyMascada.Application.Features.CsvImport.DTOs;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Service for AI-powered CSV structure analysis and column mapping
/// </summary>
public interface IAICsvAnalysisService
{
    /// <summary>
    /// Analyzes a CSV file structure using AI to suggest column mappings
    /// </summary>
    /// <param name="csvStream">The CSV file stream</param>
    /// <param name="accountType">Optional account type hint (e.g., "Checking", "Credit Card")</param>
    /// <param name="currencyHint">Optional currency hint</param>
    /// <returns>Analysis result with suggested mappings and confidence scores</returns>
    Task<CsvAnalysisResultDto> AnalyzeCsvStructureAsync(
        Stream csvStream, 
        string? accountType = null,
        string? currencyHint = null);

    /// <summary>
    /// Validates the suggested mappings against sample data
    /// </summary>
    /// <param name="csvStream">The CSV file stream</param>
    /// <param name="mappings">The column mappings to validate</param>
    /// <returns>Validation result with any warnings or errors</returns>
    Task<CsvMappingValidationResult> ValidateMappingsAsync(
        Stream csvStream,
        CsvColumnMappings mappings);
}