using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.ImportReview.DTOs;

namespace MyMascada.Application.Features.ImportReview.Commands;

public class BulkResolveConflictsCommandHandler : IRequestHandler<BulkResolveConflictsCommand, BulkActionResult>
{
    private readonly IImportAnalysisService _importAnalysisService;

    public BulkResolveConflictsCommandHandler(IImportAnalysisService importAnalysisService)
    {
        _importAnalysisService = importAnalysisService;
    }

    public async Task<BulkActionResult> Handle(BulkResolveConflictsCommand request, CancellationToken cancellationToken)
    {
        var bulkRequest = new BulkActionRequest
        {
            AnalysisId = request.AnalysisId,
            ActionType = request.ActionType,
            TargetConflictType = request.TargetConflictType,
            Resolution = request.Resolution
        };

        return await _importAnalysisService.ApplyBulkActionAsync(bulkRequest);
    }
}