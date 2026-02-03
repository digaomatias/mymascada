using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Budgets.DTOs;
using MyMascada.Application.Features.Budgets.Services;

namespace MyMascada.Application.Features.Budgets.Commands;

public class UpdateBudgetCommand : IRequest<BudgetDetailDto>
{
    public int BudgetId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsRecurring { get; set; }
    public Guid UserId { get; set; }
}

public class UpdateBudgetCommandHandler : IRequestHandler<UpdateBudgetCommand, BudgetDetailDto>
{
    private readonly IBudgetRepository _budgetRepository;
    private readonly IBudgetCalculationService _calculationService;

    public UpdateBudgetCommandHandler(
        IBudgetRepository budgetRepository,
        IBudgetCalculationService calculationService)
    {
        _budgetRepository = budgetRepository;
        _calculationService = calculationService;
    }

    public async Task<BudgetDetailDto> Handle(UpdateBudgetCommand request, CancellationToken cancellationToken)
    {
        // Get the budget
        var budget = await _budgetRepository.GetBudgetByIdAsync(request.BudgetId, request.UserId, cancellationToken);
        if (budget == null)
        {
            throw new ArgumentException("Budget not found or you don't have permission to access it.");
        }

        // Update fields if provided
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            budget.Name = request.Name.Trim();
        }

        if (request.Description != null)
        {
            budget.Description = request.Description.Trim();
        }

        if (request.IsActive.HasValue)
        {
            budget.IsActive = request.IsActive.Value;
        }

        if (request.IsRecurring.HasValue)
        {
            budget.IsRecurring = request.IsRecurring.Value;
        }

        budget.UpdatedAt = DateTime.UtcNow;

        // Save changes
        await _budgetRepository.UpdateBudgetAsync(budget, cancellationToken);

        // Return updated budget with progress
        return await _calculationService.CalculateBudgetProgressAsync(budget, request.UserId, cancellationToken);
    }
}
