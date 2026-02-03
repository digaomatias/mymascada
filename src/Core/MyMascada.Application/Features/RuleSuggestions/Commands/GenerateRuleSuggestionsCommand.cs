using MediatR;
using MyMascada.Application.Features.RuleSuggestions.DTOs;

namespace MyMascada.Application.Features.RuleSuggestions.Commands;

/// <summary>
/// Command to generate new rule suggestions for a user
/// </summary>
public class GenerateRuleSuggestionsCommand : IRequest<RuleSuggestionsResponse>
{
    public Guid UserId { get; set; }
    public int? LimitSuggestions { get; set; } = 10;
    public double? MinConfidenceThreshold { get; set; } = 0.7;
    public bool ForceRegenerate { get; set; } = false;
}