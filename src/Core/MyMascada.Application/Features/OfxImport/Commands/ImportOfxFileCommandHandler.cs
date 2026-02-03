using MediatR;
using MyMascada.Application.Features.OfxImport.DTOs;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.OfxImport.Commands;

public class ImportOfxFileCommandHandler : IRequestHandler<ImportOfxFileCommand, OfxImportResponse>
{
    private readonly IOfxImportService _ofxImportService;

    public ImportOfxFileCommandHandler(IOfxImportService ofxImportService)
    {
        _ofxImportService = ofxImportService;
    }

    public async Task<OfxImportResponse> Handle(ImportOfxFileCommand request, CancellationToken cancellationToken)
    {
        var importRequest = new OfxImportRequest
        {
            FileName = request.FileName,
            Content = request.Content,
            AccountId = request.AccountId,
            CreateAccountIfNotExists = request.CreateAccountIfNotExists,
            AccountName = request.AccountName
        };

        return await _ofxImportService.ImportOfxFileAsync(importRequest, request.UserId);
    }
}