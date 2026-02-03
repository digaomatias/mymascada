using MediatR;
using MyMascada.Application.Features.ImportReview.DTOs;

namespace MyMascada.Application.Features.ImportReview.Commands;

public record AnalyzeImportCommand : IRequest<ImportAnalysisResult>
{
    public string Source { get; init; } = string.Empty;
    public int AccountId { get; init; }
    public Guid UserId { get; init; }
    public IEnumerable<ImportCandidateDto> Candidates { get; init; } = new List<ImportCandidateDto>();
    public ImportAnalysisOptions Options { get; init; } = new();
}