using MediatR;

namespace MyMascada.Application.Features.RuleSuggestions.Commands;

/// <summary>
/// Command to accept a rule suggestion and create a categorization rule
/// </summary>
public class AcceptRuleSuggestionCommand : IRequest<int>
{
    public int SuggestionId { get; set; }
    public Guid UserId { get; set; }
    public string? CustomRuleName { get; set; }
    public string? CustomRuleDescription { get; set; }
    public int? Priority { get; set; }
}