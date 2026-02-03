using MediatR;
using MyMascada.Application.Features.CsvImport.DTOs;

namespace MyMascada.Application.Features.CsvImport.Commands;

/// <summary>
/// Command to import CSV with AI-suggested column mappings
/// </summary>
public class ImportCsvWithMappingsCommand : IRequest<CsvImportResponse>
{
    public Guid UserId { get; set; }
    public int? AccountId { get; set; }
    public string? AccountName { get; set; }
    public byte[] CsvData { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = string.Empty;
    public CsvColumnMappings Mappings { get; set; } = new();
    public bool SkipDuplicates { get; set; } = true;
    public bool AutoCategorize { get; set; } = true;
    public int MaxRows { get; set; } = 0;
}