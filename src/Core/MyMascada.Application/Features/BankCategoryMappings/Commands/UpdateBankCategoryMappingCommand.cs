using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankCategoryMappings.DTOs;

namespace MyMascada.Application.Features.BankCategoryMappings.Commands;

/// <summary>
/// Command to update an existing bank category mapping.
/// </summary>
public record UpdateBankCategoryMappingCommand : IRequest<BankCategoryMappingDto?>
{
    public int MappingId { get; init; }
    public Guid UserId { get; init; }
    public int CategoryId { get; init; }
}

public class UpdateBankCategoryMappingCommandHandler
    : IRequestHandler<UpdateBankCategoryMappingCommand, BankCategoryMappingDto?>
{
    private readonly IBankCategoryMappingService _mappingService;

    public UpdateBankCategoryMappingCommandHandler(IBankCategoryMappingService mappingService)
    {
        _mappingService = mappingService;
    }

    public async Task<BankCategoryMappingDto?> Handle(
        UpdateBankCategoryMappingCommand request,
        CancellationToken cancellationToken)
    {
        if (request.CategoryId <= 0)
        {
            throw new ArgumentException("Valid category ID is required", nameof(request.CategoryId));
        }

        // Get existing mapping to get the bank category name and provider
        var existingMapping = await _mappingService.GetByIdAsync(
            request.MappingId,
            request.UserId,
            cancellationToken);

        if (existingMapping == null)
        {
            return null;
        }

        // Use the upsert method which handles updating existing mappings
        var updatedMapping = await _mappingService.UpsertMappingAsync(
            existingMapping.BankCategoryName,
            existingMapping.ProviderId,
            request.CategoryId,
            request.UserId,
            cancellationToken);

        return new BankCategoryMappingDto
        {
            Id = updatedMapping.Id,
            BankCategoryName = updatedMapping.BankCategoryName,
            ProviderId = updatedMapping.ProviderId,
            CategoryId = updatedMapping.CategoryId,
            CategoryName = updatedMapping.Category?.Name ?? "",
            CategoryFullPath = updatedMapping.Category?.GetFullPath(),
            ConfidenceScore = updatedMapping.ConfidenceScore,
            EffectiveConfidence = updatedMapping.GetEffectiveConfidence(),
            Source = updatedMapping.Source,
            ApplicationCount = updatedMapping.ApplicationCount,
            OverrideCount = updatedMapping.OverrideCount,
            IsActive = updatedMapping.IsActive,
            CreatedAt = updatedMapping.CreatedAt,
            UpdatedAt = updatedMapping.UpdatedAt
        };
    }
}
