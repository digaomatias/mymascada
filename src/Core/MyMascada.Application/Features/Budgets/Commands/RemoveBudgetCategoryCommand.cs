using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Budgets.DTOs;
using MyMascada.Application.Features.Budgets.Services;

namespace MyMascada.Application.Features.Budgets.Commands;

public class RemoveBudgetCategoryCommand : IRequest<BudgetDetailDto>
{
    public int BudgetId { get; set; }
    public int CategoryId { get; set; }
    public Guid UserId { get; set; }
}

public class RemoveBudgetCategoryCommandHandler : IRequestHandler<RemoveBudgetCategoryCommand, BudgetDetailDto>
{
    private readonly IBudgetRepository _budgetRepository;
    private readonly IBudgetCalculationService _calculationService;

    public RemoveBudgetCategoryCommandHandler(
        IBudgetRepository budgetRepository,
        IBudgetCalculationService calculationService)
    {
        _budgetRepository = budgetRepository;
        _calculationService = calculationService;
    }

    public async Task<BudgetDetailDto> Handle(RemoveBudgetCategoryCommand request, CancellationToken cancellationToken)
    {
        // Get the budget with categories
        var budget = await _budgetRepository.GetBudgetByIdAsync(request.BudgetId, request.UserId, cancellationToken);
        if (budget == null)
        {
            throw new ArgumentException("Budget not found or you don't have permission to access it.");
        }

        // Find the budget category
        var budgetCategory = budget.BudgetCategories.FirstOrDefault(bc => bc.CategoryId == request.CategoryId && !bc.IsDeleted);
        if (budgetCategory == null)
        {
            throw new ArgumentException("Category allocation not found in this budget.");
        }

        // Soft delete the category allocation
        await _budgetRepository.RemoveBudgetCategoryAsync(request.BudgetId, request.CategoryId, cancellationToken);

        // Reload budget with updated categories
        var updatedBudget = await _budgetRepository.GetBudgetByIdAsync(request.BudgetId, request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload budget.");

        // Return updated budget with progress
        return await _calculationService.CalculateBudgetProgressAsync(updatedBudget, request.UserId, cancellationToken);
    }
}
