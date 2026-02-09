using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Transfers.DTOs;

namespace MyMascada.Infrastructure.Services;

/// <summary>
/// Redacts private account information from transfers when viewed by a sharee.
/// If the viewer cannot access an account in a transfer, its details are replaced
/// with "Private Account" to protect financial privacy.
/// </summary>
public class TransferRedactionService : ITransferRedactionService
{
    private readonly IAccountAccessService _accountAccess;

    private const string RedactedAccountName = "Private Account";

    public TransferRedactionService(IAccountAccessService accountAccess)
    {
        _accountAccess = accountAccess;
    }

    public async Task<TransferDto> RedactForViewerAsync(TransferDto transfer, Guid viewerUserId)
    {
        var accessibleIds = await _accountAccess.GetAccessibleAccountIdsAsync(viewerUserId);

        var sourceAccessible = accessibleIds.Contains(transfer.SourceAccount.Id);
        var destAccessible = accessibleIds.Contains(transfer.DestinationAccount.Id);

        // If the viewer can see both sides, no redaction needed
        if (sourceAccessible && destAccessible)
            return transfer;

        // Redact the inaccessible side
        if (!sourceAccessible)
        {
            transfer.SourceAccount = new TransferAccountDto
            {
                Id = 0,
                Name = RedactedAccountName,
                Currency = transfer.Currency,
                Type = string.Empty
            };
        }

        if (!destAccessible)
        {
            transfer.DestinationAccount = new TransferAccountDto
            {
                Id = 0,
                Name = RedactedAccountName,
                Currency = transfer.Currency,
                Type = string.Empty
            };
        }

        // Also redact transaction details for inaccessible accounts
        transfer.Transactions = transfer.Transactions
            .Where(t => accessibleIds.Contains(t.AccountId))
            .ToList();

        return transfer;
    }

    public async Task<IEnumerable<TransferDto>> RedactForViewerAsync(IEnumerable<TransferDto> transfers, Guid viewerUserId)
    {
        var result = new List<TransferDto>();
        foreach (var transfer in transfers)
        {
            result.Add(await RedactForViewerAsync(transfer, viewerUserId));
        }
        return result;
    }
}
