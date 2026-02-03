using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Budgets.DTOs;
using MyMascada.Application.Features.Budgets.Services;

namespace MyMascada.Application.Features.Budgets.Commands;

public class UpdateBudgetCategoryCommand : IRequest<BudgetDetailDto>
{
    public int BudgetId { get; set; }
    public int CategoryId { get; set; }
    public decimal? BudgetedAmount { get; set; }
    public bool? AllowRollover { get; set; }
    public bool? CarryOverspend { get; set; }
    public bool? IncludeSubcategories { get; set; }
    public string? Notes { get; set; }
    public Guid UserId { get; set; }
}

public class UpdateBudgetCategoryCommandHandler : IRequestHandler<UpdateBudgetCategoryCommand, BudgetDetailDto>
{
    private readonly IBudgetRepository _budgetRepository;
    private readonly IBudgetCalculationService _calculationService;

    public UpdateBudgetCategoryCommandHandler(
        IBudgetRepository budgetRepository,
        IBudgetCalculationService calculationService)
    {
        _budgetRepository = budgetRepository;
        _calculationService = calculationService;
    }

    public async Task<BudgetDetailDto> Handle(UpdateBudgetCategoryCommand request, CancellationToken cancellationToken)
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

        // Update fields if provided
        if (request.BudgetedAmount.HasValue)
        {
            budgetCategory.BudgetedAmount = request.BudgetedAmount.Value;
        }

        if (request.AllowRollover.HasValue)
        {
            budgetCategory.AllowRollover = request.AllowRollover.Value;
        }

        if (request.CarryOverspend.HasValue)
        {
            budgetCategory.CarryOverspend = request.CarryOverspend.Value;
        }

        if (request.IncludeSubcategories.HasValue)
        {
            budgetCategory.IncludeSubcategories = request.IncludeSubcategories.Value;
        }

        if (request.Notes != null)
        {
            budgetCategory.Notes = request.Notes.Trim();
        }

        budgetCategory.UpdatedAt = DateTime.UtcNow;

        // Save changes
        await _budgetRepository.UpsertBudgetCategoryAsync(budgetCategory, cancellationToken);

        // Reload budget with updated categories
        var updatedBudget = await _budgetRepository.GetBudgetByIdAsync(request.BudgetId, request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload budget.");

        // Return updated budget with progress
        return await _calculationService.CalculateBudgetProgressAsync(updatedBudget, request.UserId, cancellationToken);
    }
}
