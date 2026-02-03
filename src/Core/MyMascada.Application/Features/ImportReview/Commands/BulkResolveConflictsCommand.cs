using MediatR;
using MyMascada.Application.Features.ImportReview.DTOs;

namespace MyMascada.Application.Features.ImportReview.Commands;

public record BulkResolveConflictsCommand : IRequest<BulkActionResult>
{
    public string AnalysisId { get; init; } = string.Empty;
    public BulkActionType ActionType { get; init; }
    public ConflictType? TargetConflictType { get; init; }
    public ConflictResolution Resolution { get; init; }
}