using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.ImportReview.DTOs;

namespace MyMascada.Application.Features.ImportReview.Commands;

public class AnalyzeImportCommandHandler : IRequestHandler<AnalyzeImportCommand, ImportAnalysisResult>
{
    private readonly IImportAnalysisService _importAnalysisService;

    public AnalyzeImportCommandHandler(IImportAnalysisService importAnalysisService)
    {
        _importAnalysisService = importAnalysisService;
    }

    public async Task<ImportAnalysisResult> Handle(AnalyzeImportCommand request, CancellationToken cancellationToken)
    {
        var analysisRequest = new AnalyzeImportRequest
        {
            Source = request.Source,
            AccountId = request.AccountId,
            UserId = request.UserId,
            Candidates = request.Candidates,
            Options = request.Options
        };

        return await _importAnalysisService.AnalyzeImportAsync(analysisRequest);
    }
}