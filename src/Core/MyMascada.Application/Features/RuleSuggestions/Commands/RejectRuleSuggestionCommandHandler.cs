using MediatR;
using MyMascada.Application.Features.RuleSuggestions.Services;

namespace MyMascada.Application.Features.RuleSuggestions.Commands;

/// <summary>
/// Handler for rejecting/dismissing a rule suggestion
/// </summary>
public class RejectRuleSuggestionCommandHandler : IRequestHandler<RejectRuleSuggestionCommand, Unit>
{
    private readonly IRuleSuggestionService _ruleSuggestionService;

    public RejectRuleSuggestionCommandHandler(IRuleSuggestionService ruleSuggestionService)
    {
        _ruleSuggestionService = ruleSuggestionService;
    }

    public async Task<Unit> Handle(RejectRuleSuggestionCommand request, CancellationToken cancellationToken)
    {
        await _ruleSuggestionService.RejectSuggestionAsync(request.SuggestionId, request.UserId);
        return Unit.Value;
    }
}