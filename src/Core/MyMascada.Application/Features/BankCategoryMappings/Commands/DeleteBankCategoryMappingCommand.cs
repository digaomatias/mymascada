using MediatR;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.BankCategoryMappings.Commands;

/// <summary>
/// Command to delete a bank category mapping.
/// </summary>
public record DeleteBankCategoryMappingCommand : IRequest<bool>
{
    public int MappingId { get; init; }
    public Guid UserId { get; init; }
}

public class DeleteBankCategoryMappingCommandHandler
    : IRequestHandler<DeleteBankCategoryMappingCommand, bool>
{
    private readonly IBankCategoryMappingService _mappingService;

    public DeleteBankCategoryMappingCommandHandler(IBankCategoryMappingService mappingService)
    {
        _mappingService = mappingService;
    }

    public async Task<bool> Handle(
        DeleteBankCategoryMappingCommand request,
        CancellationToken cancellationToken)
    {
        return await _mappingService.DeleteMappingAsync(
            request.MappingId,
            request.UserId,
            cancellationToken);
    }
}
