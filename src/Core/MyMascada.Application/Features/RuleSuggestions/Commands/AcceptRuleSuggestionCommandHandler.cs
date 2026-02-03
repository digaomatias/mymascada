using MediatR;
using MyMascada.Application.Features.RuleSuggestions.Services;

namespace MyMascada.Application.Features.RuleSuggestions.Commands;

/// <summary>
/// Handler for accepting a rule suggestion and creating a categorization rule
/// </summary>
public class AcceptRuleSuggestionCommandHandler : IRequestHandler<AcceptRuleSuggestionCommand, int>
{
    private readonly IRuleSuggestionService _ruleSuggestionService;

    public AcceptRuleSuggestionCommandHandler(IRuleSuggestionService ruleSuggestionService)
    {
        _ruleSuggestionService = ruleSuggestionService;
    }

    public async Task<int> Handle(AcceptRuleSuggestionCommand request, CancellationToken cancellationToken)
    {
        var ruleId = await _ruleSuggestionService.AcceptSuggestionAsync(
            request.SuggestionId,
            request.UserId,
            request.CustomRuleName,
            request.CustomRuleDescription,
            request.Priority);

        return ruleId;
    }
}