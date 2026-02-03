using MyMascada.Application.Features.OfxImport.DTOs;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Service for parsing OFX/QFX files without importing to database.
/// Separated from import logic for reusability in reconciliation and validation scenarios.
/// </summary>
public interface IOfxParserService
{
    /// <summary>
    /// Parse OFX file content into structured transaction and account data
    /// </summary>
    /// <param name="content">OFX file content as string</param>
    /// <returns>Parse result with transactions, account info, errors, and warnings</returns>
    Task<OfxParseResult> ParseOfxFileAsync(string content);

    /// <summary>
    /// Parse OFX file stream into structured data
    /// </summary>
    /// <param name="stream">OFX file stream</param>
    /// <returns>Parse result with transactions, account info, errors, and warnings</returns>
    Task<OfxParseResult> ParseOfxFileAsync(Stream stream);

    /// <summary>
    /// Validate OFX file format without full parsing
    /// </summary>
    /// <param name="content">OFX file content</param>
    /// <returns>True if file appears to be valid OFX</returns>
    bool IsValidOfxFile(string content);

    /// <summary>
    /// Validate OFX file stream format
    /// </summary>
    /// <param name="stream">OFX file stream</param>
    /// <returns>True if file appears to be valid OFX</returns>
    Task<bool> IsValidOfxFileAsync(Stream stream);

    /// <summary>
    /// Extract just the XML content from OFX file (removing headers)
    /// </summary>
    /// <param name="content">Full OFX file content</param>
    /// <returns>XML portion of the OFX file</returns>
    string ExtractXmlContent(string content);
}