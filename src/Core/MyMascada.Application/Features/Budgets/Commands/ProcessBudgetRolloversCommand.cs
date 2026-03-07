using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Budgets.DTOs;
using MyMascada.Application.Features.Budgets.Services;

namespace MyMascada.Application.Features.Budgets.Commands;

/// <summary>
/// Legacy command kept for backward compatibility with the API endpoint.
/// Delegates to ProcessExpiredBudgetsCommand which handles all expired budgets.
/// </summary>
public class ProcessBudgetRolloversCommand : IRequest<BudgetRolloverResultDto>
{
    public Guid UserId { get; set; }

    /// <summary>
    /// If true, only preview what would be rolled over without making changes
    /// </summary>
    public bool PreviewOnly { get; set; } = false;
}

public class ProcessBudgetRolloversCommandHandler : IRequestHandler<ProcessBudgetRolloversCommand, BudgetRolloverResultDto>
{
    private readonly IMediator _mediator;

    public ProcessBudgetRolloversCommandHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<BudgetRolloverResultDto> Handle(ProcessBudgetRolloversCommand request, CancellationToken cancellationToken)
    {
        // Delegate to the new command
        return await _mediator.Send(new ProcessExpiredBudgetsCommand
        {
            UserId = request.UserId,
            PreviewOnly = request.PreviewOnly
        }, cancellationToken);
    }
}
