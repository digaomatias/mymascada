using MediatR;

namespace MyMascada.Application.Features.RuleSuggestions.Commands;

/// <summary>
/// Command to reject/dismiss a rule suggestion
/// </summary>
public class RejectRuleSuggestionCommand : IRequest<Unit>
{
    public int SuggestionId { get; set; }
    public Guid UserId { get; set; }
}