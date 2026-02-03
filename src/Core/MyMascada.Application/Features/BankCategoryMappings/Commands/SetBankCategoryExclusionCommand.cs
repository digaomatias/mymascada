using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankCategoryMappings.DTOs;

namespace MyMascada.Application.Features.BankCategoryMappings.Commands;

/// <summary>
/// Command to set the exclusion status of a bank category mapping.
/// When excluded, transactions with this bank category will not be
/// auto-categorized by the BankCategoryHandler.
/// </summary>
public record SetBankCategoryExclusionCommand : IRequest<BankCategoryMappingDto?>
{
    public int MappingId { get; init; }
    public Guid UserId { get; init; }
    public bool IsExcluded { get; init; }
}

public class SetBankCategoryExclusionCommandHandler
    : IRequestHandler<SetBankCategoryExclusionCommand, BankCategoryMappingDto?>
{
    private readonly IBankCategoryMappingRepository _mappingRepository;

    public SetBankCategoryExclusionCommandHandler(IBankCategoryMappingRepository mappingRepository)
    {
        _mappingRepository = mappingRepository;
    }

    public async Task<BankCategoryMappingDto?> Handle(
        SetBankCategoryExclusionCommand request,
        CancellationToken cancellationToken)
    {
        // Get existing mapping (includes Category navigation property)
        var mapping = await _mappingRepository.GetByIdAsync(
            request.MappingId,
            request.UserId,
            cancellationToken);

        if (mapping == null)
        {
            return null;
        }

        // Update exclusion status
        mapping.IsExcluded = request.IsExcluded;
        mapping.UpdatedAt = DateTime.UtcNow;

        await _mappingRepository.UpdateAsync(mapping, cancellationToken);

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
