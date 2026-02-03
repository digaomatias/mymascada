using MediatR;
using MyMascada.Application.Features.OfxImport.DTOs;

namespace MyMascada.Application.Features.OfxImport.Commands;

public class ImportOfxFileCommand : IRequest<OfxImportResponse>
{
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int? AccountId { get; set; }
    public bool CreateAccountIfNotExists { get; set; } = false;
    public string? AccountName { get; set; }
    public string UserId { get; set; } = string.Empty;
}