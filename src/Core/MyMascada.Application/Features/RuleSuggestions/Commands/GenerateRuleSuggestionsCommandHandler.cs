using MediatR;
using MyMascada.Application.Features.RuleSuggestions.DTOs;
using MyMascada.Application.Features.RuleSuggestions.Services;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.RuleSuggestions.Commands;

/// <summary>
/// Handler for generating new rule suggestions
/// </summary>
public class GenerateRuleSuggestionsCommandHandler : IRequestHandler<GenerateRuleSuggestionsCommand, RuleSuggestionsResponse>
{
    private readonly IRuleSuggestionService _ruleSuggestionService;

    public GenerateRuleSuggestionsCommandHandler(IRuleSuggestionService ruleSuggestionService)
    {
        _ruleSuggestionService = ruleSuggestionService;
    }

    public async Task<RuleSuggestionsResponse> Handle(GenerateRuleSuggestionsCommand request, CancellationToken cancellationToken)
    {
        var suggestions = await _ruleSuggestionService.GenerateSuggestionsAsync(
            request.UserId, 
            request.LimitSuggestions ?? 10, 
            request.MinConfidenceThreshold ?? 0.7);

        var summary = await _ruleSuggestionService.GetSummaryAsync(request.UserId);

        var suggestionDtos = suggestions.Select(ConvertToDto).ToList();

        return new RuleSuggestionsResponse
        {
            Summary = summary,
            Suggestions = suggestionDtos
        };
    }

    private static RuleSuggestionDto ConvertToDto(RuleSuggestion suggestion)
    {
        return new RuleSuggestionDto
        {
            Id = suggestion.Id,
            Name = suggestion.Name,
            Description = suggestion.Description,
            Pattern = suggestion.Pattern,
            Type = suggestion.Type,
            IsCaseSensitive = suggestion.IsCaseSensitive,
            ConfidenceScore = suggestion.ConfidenceScore,
            MatchCount = suggestion.MatchCount,
            GenerationMethod = suggestion.GenerationMethod,
            SuggestedCategoryId = suggestion.SuggestedCategoryId,
            SuggestedCategoryName = suggestion.SuggestedCategory?.Name ?? "Unknown",
            SuggestedCategoryColor = suggestion.SuggestedCategory?.Color,
            SuggestedCategoryIcon = suggestion.SuggestedCategory?.Icon,
            CreatedAt = suggestion.CreatedAt,
            SampleTransactions = suggestion.SampleTransactions
                .OrderBy(s => s.SortOrder)
                .Select(s => new RuleSuggestionSampleDto
                {
                    TransactionId = s.TransactionId,
                    Description = s.Description,
                    Amount = s.Amount,
                    TransactionDate = s.TransactionDate,
                    AccountName = s.AccountName,
                    SortOrder = s.SortOrder
                })
                .ToList()
        };
    }
}