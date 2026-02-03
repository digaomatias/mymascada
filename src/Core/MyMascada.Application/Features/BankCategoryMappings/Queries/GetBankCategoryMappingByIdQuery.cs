using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankCategoryMappings.DTOs;

namespace MyMascada.Application.Features.BankCategoryMappings.Queries;

/// <summary>
/// Query to get a single bank category mapping by ID.
/// </summary>
public record GetBankCategoryMappingByIdQuery : IRequest<BankCategoryMappingDto?>
{
    public int MappingId { get; init; }
    public Guid UserId { get; init; }
}

public class GetBankCategoryMappingByIdQueryHandler
    : IRequestHandler<GetBankCategoryMappingByIdQuery, BankCategoryMappingDto?>
{
    private readonly IBankCategoryMappingService _mappingService;

    public GetBankCategoryMappingByIdQueryHandler(IBankCategoryMappingService mappingService)
    {
        _mappingService = mappingService;
    }

    public async Task<BankCategoryMappingDto?> Handle(
        GetBankCategoryMappingByIdQuery request,
        CancellationToken cancellationToken)
    {
        var mapping = await _mappingService.GetByIdAsync(
            request.MappingId,
            request.UserId,
            cancellationToken);

        if (mapping == null)
            return null;

        return new BankCategoryMappingDto
        {
            Id = mapping.Id,
            BankCategoryName = mapping.BankCategoryName,
            ProviderId = mapping.ProviderId,
            CategoryId = mapping.CategoryId,
            CategoryName = mapping.Category?.Name ?? "",
            CategoryFullPath = mapping.Category?.GetFullPath(),
            ConfidenceScore = mapping.ConfidenceScore,
            EffectiveConfidence = mapping.GetEffectiveConfidence(),
            Source = mapping.Source,
            ApplicationCount = mapping.ApplicationCount,
            OverrideCount = mapping.OverrideCount,
            IsActive = mapping.IsActive,
            IsExcluded = mapping.IsExcluded,
            CreatedAt = mapping.CreatedAt,
            UpdatedAt = mapping.UpdatedAt
        };
    }
}
