using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Budgets.DTOs;
using MyMascada.Application.Features.Budgets.Services;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Budgets.Commands;

public class AddBudgetCategoryCommand : IRequest<BudgetDetailDto>
{
    public int BudgetId { get; set; }
    public int CategoryId { get; set; }
    public decimal BudgetedAmount { get; set; }
    public bool AllowRollover { get; set; } = false;
    public bool CarryOverspend { get; set; } = false;
    public bool IncludeSubcategories { get; set; } = true;
    public string? Notes { get; set; }
    public Guid UserId { get; set; }
}

public class AddBudgetCategoryCommandHandler : IRequestHandler<AddBudgetCategoryCommand, BudgetDetailDto>
{
    private readonly IBudgetRepository _budgetRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IBudgetCalculationService _calculationService;

    public AddBudgetCategoryCommandHandler(
        IBudgetRepository budgetRepository,
        ICategoryRepository categoryRepository,
        IBudgetCalculationService calculationService)
    {
        _budgetRepository = budgetRepository;
        _categoryRepository = categoryRepository;
        _calculationService = calculationService;
    }

    public async Task<BudgetDetailDto> Handle(AddBudgetCategoryCommand request, CancellationToken cancellationToken)
    {
        // Get the budget
        var budget = await _budgetRepository.GetBudgetByIdAsync(request.BudgetId, request.UserId, cancellationToken);
        if (budget == null)
        {
            throw new ArgumentException("Budget not found or you don't have permission to access it.");
        }

        // Validate category exists and belongs to user
        var category = await _categoryRepository.GetByIdAsync(request.CategoryId);
        if (category == null || category.UserId != request.UserId)
        {
            throw new ArgumentException("Category not found or you don't have permission to access it.");
        }

        // Check if category already exists in budget
        if (budget.BudgetCategories.Any(bc => bc.CategoryId == request.CategoryId && !bc.IsDeleted))
        {
            throw new ArgumentException("This category is already allocated in this budget.");
        }

        // Create budget category
        var budgetCategory = new BudgetCategory
        {
            BudgetId = request.BudgetId,
            CategoryId = request.CategoryId,
            BudgetedAmount = request.BudgetedAmount,
            AllowRollover = request.AllowRollover,
            CarryOverspend = request.CarryOverspend,
            IncludeSubcategories = request.IncludeSubcategories,
            Notes = request.Notes?.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _budgetRepository.UpsertBudgetCategoryAsync(budgetCategory, cancellationToken);

        // Reload budget with updated categories
        var updatedBudget = await _budgetRepository.GetBudgetByIdAsync(request.BudgetId, request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload budget.");

        // Return updated budget with progress
        return await _calculationService.CalculateBudgetProgressAsync(updatedBudget, request.UserId, cancellationToken);
    }
}
