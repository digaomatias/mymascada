using MyMascada.Application.Features.CsvImport.DTOs;

namespace MyMascada.Application.Common.Interfaces;

public interface ICsvImportService
{
    Task<CsvParseResult> ParseCsvAsync(Stream csvStream, CsvFieldMapping mapping, bool hasHeader = true);
    CsvFieldMapping GetDefaultMapping(CsvFormat format);
    Task<bool> ValidateFileAsync(Stream csvStream);
    string GenerateExternalId(CsvTransactionRow row);
}