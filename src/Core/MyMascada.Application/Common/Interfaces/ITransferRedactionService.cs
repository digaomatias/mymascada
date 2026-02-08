using MyMascada.Application.Features.Transfers.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Redacts transfer details when one side crosses a shared/private account boundary.
/// When a sharee views a transfer involving a shared account, the private (non-accessible)
/// account's details are replaced with "Private Account".
/// </summary>
public interface ITransferRedactionService
{
    /// <summary>
    /// Redacts transfer DTO fields based on which accounts the viewer can access.
    /// </summary>
    Task<TransferDto> RedactForViewerAsync(TransferDto transfer, Guid viewerUserId);

    /// <summary>
    /// Redacts a list of transfer DTOs for the viewer.
    /// </summary>
    Task<IEnumerable<TransferDto>> RedactForViewerAsync(IEnumerable<TransferDto> transfers, Guid viewerUserId);
}
