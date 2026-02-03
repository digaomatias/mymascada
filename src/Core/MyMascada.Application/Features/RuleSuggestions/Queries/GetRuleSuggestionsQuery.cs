using MediatR;
using MyMascada.Application.Features.RuleSuggestions.DTOs;

namespace MyMascada.Application.Features.RuleSuggestions.Queries;

/// <summary>
/// Query to get rule suggestions for a user
/// </summary>
public class GetRuleSuggestionsQuery : IRequest<RuleSuggestionsResponse>
{
    public Guid UserId { get; set; }
    public bool IncludeProcessed { get; set; } = false;
    public int? Limit { get; set; }
    public double? MinConfidenceThreshold { get; set; }
}