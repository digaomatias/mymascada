using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.DTOs;

namespace MyMascada.Application.Features.BankConnections.Queries;

/// <summary>
/// Gets the current status of a background bank sync job.
/// </summary>
public record GetBankSyncJobStatusQuery(
    Guid UserId,
    string JobId
) : IRequest<BankSyncJobStatusDto>;

public class GetBankSyncJobStatusQueryHandler
    : IRequestHandler<GetBankSyncJobStatusQuery, BankSyncJobStatusDto>
{
    private readonly IBankSyncJobService _bankSyncJobService;

    public GetBankSyncJobStatusQueryHandler(IBankSyncJobService bankSyncJobService)
    {
        _bankSyncJobService = bankSyncJobService ?? throw new ArgumentNullException(nameof(bankSyncJobService));
    }

    public Task<BankSyncJobStatusDto> Handle(GetBankSyncJobStatusQuery request, CancellationToken cancellationToken)
    {
        var status = _bankSyncJobService.GetStatus(request.JobId, request.UserId);
        return Task.FromResult(status);
    }
}
