using MyMascada.Application.Features.CsvImport.DTOs;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Service for parsing CSV files without importing to database.
/// Separated from import logic for reusability in reconciliation and preview scenarios.
/// </summary>
public interface ICsvParserService
{
    /// <summary>
    /// Parse CSV file stream into structured transaction data
    /// </summary>
    /// <param name="csvStream">CSV file stream</param>
    /// <param name="mapping">Field mapping configuration for the CSV format</param>
    /// <param name="hasHeader">Whether the CSV file has header row</param>
    /// <param name="maxRows">Maximum number of rows to parse (0 = all rows)</param>
    /// <returns>Parse result with transactions, errors, and warnings</returns>
    Task<CsvParseResult> ParseCsvAsync(Stream csvStream, CsvFieldMapping mapping, bool hasHeader = true, int maxRows = 0);

    /// <summary>
    /// Validate CSV file format without full parsing
    /// </summary>
    /// <param name="csvStream">CSV file stream</param>
    /// <returns>True if file appears to be valid CSV</returns>
    Task<bool> ValidateFileAsync(Stream csvStream);

    /// <summary>
    /// Generate external ID for transaction deduplication
    /// </summary>
    /// <param name="row">Parsed transaction row</param>
    /// <returns>SHA256-based external ID</returns>
    string GenerateExternalId(CsvTransactionRow row);

    /// <summary>
    /// Get default field mapping for supported CSV formats
    /// </summary>
    /// <param name="format">CSV format (bank type)</param>
    /// <returns>Field mapping configuration</returns>
    CsvFieldMapping GetDefaultMapping(CsvFormat format);

    /// <summary>
    /// Get all supported CSV formats with their display names
    /// </summary>
    /// <returns>Dictionary of format enum to display information</returns>
    Dictionary<CsvFormat, string> GetSupportedFormats();
}