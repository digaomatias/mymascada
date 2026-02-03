using MediatR;
using MyMascada.Application.Features.ImportReview.DTOs;

namespace MyMascada.Application.Features.ImportReview.Commands;

public record ExecuteImportCommand : IRequest<ImportExecutionResult>
{
    public string AnalysisId { get; init; } = string.Empty;
    public int AccountId { get; init; } = 0; // Added to avoid dependency on cached analysis
    public Guid UserId { get; init; }
    public IEnumerable<ImportDecisionDto> Decisions { get; init; } = new List<ImportDecisionDto>();
    public bool SkipValidation { get; init; } = false;
}