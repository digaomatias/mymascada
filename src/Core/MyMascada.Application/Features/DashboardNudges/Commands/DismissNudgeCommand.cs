using MediatR;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.DashboardNudges.Commands;

public class DismissNudgeCommand : IRequest<Unit>
{
    public Guid UserId { get; set; }
    public string NudgeType { get; set; } = string.Empty;
    public int SnoozeDays { get; set; } = 7;
}

public class DismissNudgeCommandHandler : IRequestHandler<DismissNudgeCommand, Unit>
{
    private readonly IDashboardNudgeDismissalRepository _repository;

    public DismissNudgeCommandHandler(IDashboardNudgeDismissalRepository repository)
    {
        _repository = repository;
    }

    public async Task<Unit> Handle(DismissNudgeCommand request, CancellationToken cancellationToken)
    {
        await _repository.DismissNudgeAsync(request.UserId, request.NudgeType, request.SnoozeDays, cancellationToken);
        return Unit.Value;
    }
}
