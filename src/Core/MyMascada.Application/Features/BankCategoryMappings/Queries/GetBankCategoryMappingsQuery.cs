using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankCategoryMappings.DTOs;

namespace MyMascada.Application.Features.BankCategoryMappings.Queries;

/// <summary>
/// Query to get all bank category mappings for a user.
/// </summary>
public record GetBankCategoryMappingsQuery : IRequest<BankCategoryMappingsListDto>
{
    public Guid UserId { get; init; }
    public string? ProviderId { get; init; }
    public bool ActiveOnly { get; init; } = true;
}

public class GetBankCategoryMappingsQueryHandler
    : IRequestHandler<GetBankCategoryMappingsQuery, BankCategoryMappingsListDto>
{
    private readonly IBankCategoryMappingService _mappingService;

    public GetBankCategoryMappingsQueryHandler(IBankCategoryMappingService mappingService)
    {
        _mappingService = mappingService;
    }

    public async Task<BankCategoryMappingsListDto> Handle(
        GetBankCategoryMappingsQuery request,
        CancellationToken cancellationToken)
    {
        var mappings = await _mappingService.GetUserMappingsAsync(
            request.UserId,
            request.ProviderId,
            cancellationToken);

        var mappingList = mappings.ToList();

        // Calculate statistics
        var statistics = new MappingStatisticsDto
        {
            TotalMappings = mappingList.Count,
            AICreatedMappings = mappingList.Count(m => m.Source == "AI"),
            UserCreatedMappings = mappingList.Count(m => m.Source == "User"),
            LearnedMappings = mappingList.Count(m => m.Source == "Learned"),
            HighConfidenceCount = mappingList.Count(m => m.GetEffectiveConfidence() >= 0.9m),
            LowConfidenceCount = mappingList.Count(m => m.GetEffectiveConfidence() < 0.9m),
            TotalApplications = mappingList.Sum(m => m.ApplicationCount),
            TotalOverrides = mappingList.Sum(m => m.OverrideCount)
        };

        // Map to DTOs
        var dtos = mappingList.Select(m => new BankCategoryMappingDto
        {
            Id = m.Id,
            BankCategoryName = m.BankCategoryName,
            ProviderId = m.ProviderId,
            CategoryId = m.CategoryId,
            CategoryName = m.Category?.Name ?? "",
            CategoryFullPath = m.Category?.GetFullPath(),
            ConfidenceScore = m.ConfidenceScore,
            EffectiveConfidence = m.GetEffectiveConfidence(),
            Source = m.Source,
            ApplicationCount = m.ApplicationCount,
            OverrideCount = m.OverrideCount,
            IsActive = m.IsActive,
            IsExcluded = m.IsExcluded,
            CreatedAt = m.CreatedAt,
            UpdatedAt = m.UpdatedAt
        }).ToList();

        return new BankCategoryMappingsListDto
        {
            Mappings = dtos,
            TotalCount = dtos.Count,
            Statistics = statistics
        };
    }
}
