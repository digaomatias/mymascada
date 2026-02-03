using MyMascada.Application.Features.OfxImport.DTOs;

namespace MyMascada.Application.Common.Interfaces;

public interface IOfxImportService
{
    Task<OfxImportResponse> ImportOfxFileAsync(OfxImportRequest request, string userId);
}