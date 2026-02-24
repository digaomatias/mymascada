using MediatR;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.DashboardNudges.Queries;

public class GetDismissedNudgesQuery : IRequest<IEnumerable<string>>
{
    public Guid UserId { get; set; }
}

public class GetDismissedNudgesQueryHandler : IRequestHandler<GetDismissedNudgesQuery, IEnumerable<string>>
{
    private readonly IDashboardNudgeDismissalRepository _repository;

    public GetDismissedNudgesQueryHandler(IDashboardNudgeDismissalRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<string>> Handle(GetDismissedNudgesQuery request, CancellationToken cancellationToken)
    {
        return await _repository.GetActiveDismissedNudgeTypesAsync(request.UserId, cancellationToken);
    }
}
