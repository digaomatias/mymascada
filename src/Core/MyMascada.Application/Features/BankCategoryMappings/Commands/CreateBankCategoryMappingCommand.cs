using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankCategoryMappings.DTOs;

namespace MyMascada.Application.Features.BankCategoryMappings.Commands;

/// <summary>
/// Command to create a new bank category mapping (user-created).
/// </summary>
public record CreateBankCategoryMappingCommand : IRequest<BankCategoryMappingDto>
{
    public Guid UserId { get; init; }
    public string BankCategoryName { get; init; } = string.Empty;
    public string ProviderId { get; init; } = "akahu";
    public int CategoryId { get; init; }
}

public class CreateBankCategoryMappingCommandHandler
    : IRequestHandler<CreateBankCategoryMappingCommand, BankCategoryMappingDto>
{
    private readonly IBankCategoryMappingService _mappingService;

    public CreateBankCategoryMappingCommandHandler(IBankCategoryMappingService mappingService)
    {
        _mappingService = mappingService;
    }

    public async Task<BankCategoryMappingDto> Handle(
        CreateBankCategoryMappingCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.BankCategoryName))
        {
            throw new ArgumentException("Bank category name is required", nameof(request.BankCategoryName));
        }

        if (request.CategoryId <= 0)
        {
            throw new ArgumentException("Valid category ID is required", nameof(request.CategoryId));
        }

        var mapping = await _mappingService.UpsertMappingAsync(
            request.BankCategoryName,
            request.ProviderId,
            request.CategoryId,
            request.UserId,
            cancellationToken);

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
            CreatedAt = mapping.CreatedAt,
            UpdatedAt = mapping.UpdatedAt
        };
    }
}
