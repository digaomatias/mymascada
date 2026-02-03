using MediatR;
using MyMascada.Application.Features.RuleSuggestions.DTOs;
using MyMascada.Application.Features.RuleSuggestions.Services;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.RuleSuggestions.Queries;

/// <summary>
/// Handler for getting rule suggestions for a user
/// </summary>
public class GetRuleSuggestionsQueryHandler : IRequestHandler<GetRuleSuggestionsQuery, RuleSuggestionsResponse>
{
    private readonly IRuleSuggestionService _ruleSuggestionService;

    public GetRuleSuggestionsQueryHandler(IRuleSuggestionService ruleSuggestionService)
    {
        _ruleSuggestionService = ruleSuggestionService;
    }

    public async Task<RuleSuggestionsResponse> Handle(GetRuleSuggestionsQuery request, CancellationToken cancellationToken)
    {
        // Get existing suggestions
        var suggestions = await _ruleSuggestionService.GetSuggestionsAsync(request.UserId, includeSamples: true);

        // If no suggestions exist, generate new ones
        if (!suggestions.Any())
        {
            var minConfidence = request.MinConfidenceThreshold ?? 0.7;
            var limit = request.Limit ?? 10;
            
            suggestions = await _ruleSuggestionService.GenerateSuggestionsAsync(
                request.UserId, 
                limit, 
                minConfidence);
        }

        // Apply filters
        if (request.MinConfidenceThreshold.HasValue)
        {
            suggestions = suggestions.Where(s => s.ConfidenceScore >= request.MinConfidenceThreshold.Value).ToList();
        }

        if (request.Limit.HasValue)
        {
            suggestions = suggestions.Take(request.Limit.Value).ToList();
        }

        // Get summary
        var summary = await _ruleSuggestionService.GetSummaryAsync(request.UserId);

        // Convert to DTOs
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