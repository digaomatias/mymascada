using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.UpcomingBills.DTOs;

namespace MyMascada.Application.Features.UpcomingBills.Queries;

/// <summary>
/// Query to get upcoming bills based on recurring transaction patterns
/// </summary>
public class GetUpcomingBillsQuery : IRequest<UpcomingBillsResponse>
{
    public Guid UserId { get; set; }
    public int DaysAhead { get; set; } = 7;
}

public class GetUpcomingBillsQueryHandler : IRequestHandler<GetUpcomingBillsQuery, UpcomingBillsResponse>
{
    private readonly IRecurringPatternService _recurringPatternService;

    public GetUpcomingBillsQueryHandler(IRecurringPatternService recurringPatternService)
    {
        _recurringPatternService = recurringPatternService;
    }

    public async Task<UpcomingBillsResponse> Handle(GetUpcomingBillsQuery request, CancellationToken cancellationToken)
    {
        return await _recurringPatternService.GetUpcomingBillsAsync(
            request.UserId,
            request.DaysAhead,
            cancellationToken);
    }
}
