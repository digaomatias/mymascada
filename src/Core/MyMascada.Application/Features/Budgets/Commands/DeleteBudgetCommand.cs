using MediatR;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.Budgets.Commands;

public class DeleteBudgetCommand : IRequest<Unit>
{
    public int BudgetId { get; set; }
    public Guid UserId { get; set; }
}

public class DeleteBudgetCommandHandler : IRequestHandler<DeleteBudgetCommand, Unit>
{
    private readonly IBudgetRepository _budgetRepository;

    public DeleteBudgetCommandHandler(IBudgetRepository budgetRepository)
    {
        _budgetRepository = budgetRepository;
    }

    public async Task<Unit> Handle(DeleteBudgetCommand request, CancellationToken cancellationToken)
    {
        // Get the budget
        var budget = await _budgetRepository.GetBudgetByIdAsync(request.BudgetId, request.UserId, cancellationToken);
        if (budget == null)
        {
            throw new ArgumentException("Budget not found or you don't have permission to access it.");
        }

        // Soft delete the budget
        await _budgetRepository.DeleteBudgetAsync(request.BudgetId, request.UserId, cancellationToken);

        return Unit.Value;
    }
}
