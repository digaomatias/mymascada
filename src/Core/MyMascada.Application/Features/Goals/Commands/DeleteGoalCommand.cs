using MediatR;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.Goals.Commands;

public class DeleteGoalCommand : IRequest<Unit>
{
    public int GoalId { get; set; }
    public Guid UserId { get; set; }
}

public class DeleteGoalCommandHandler : IRequestHandler<DeleteGoalCommand, Unit>
{
    private readonly IGoalRepository _goalRepository;

    public DeleteGoalCommandHandler(IGoalRepository goalRepository)
    {
        _goalRepository = goalRepository;
    }

    public async Task<Unit> Handle(DeleteGoalCommand request, CancellationToken cancellationToken)
    {
        var goal = await _goalRepository.GetGoalByIdAsync(request.GoalId, request.UserId, cancellationToken);
        if (goal == null)
        {
            throw new ArgumentException("Goal not found or you don't have permission to access it.");
        }

        await _goalRepository.DeleteGoalAsync(request.GoalId, request.UserId, cancellationToken);

        return Unit.Value;
    }
}
