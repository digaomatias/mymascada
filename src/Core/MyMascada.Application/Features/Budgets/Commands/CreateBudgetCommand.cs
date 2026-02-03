using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Budgets.DTOs;
using MyMascada.Application.Features.Budgets.Services;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Budgets.Commands;

public class CreateBudgetCommand : IRequest<BudgetDetailDto>
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PeriodType { get; set; } = "Monthly";
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsRecurring { get; set; } = true;
    public List<CreateBudgetCategoryRequest> Categories { get; set; } = new();
    public Guid UserId { get; set; }
}

public class CreateBudgetCommandHandler : IRequestHandler<CreateBudgetCommand, BudgetDetailDto>
{
    private readonly IBudgetRepository _budgetRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IBudgetCalculationService _calculationService;

    public CreateBudgetCommandHandler(
        IBudgetRepository budgetRepository,
        ICategoryRepository categoryRepository,
        IBudgetCalculationService calculationService)
    {
        _budgetRepository = budgetRepository;
        _categoryRepository = categoryRepository;
        _calculationService = calculationService;
    }

    public async Task<BudgetDetailDto> Handle(CreateBudgetCommand request, CancellationToken cancellationToken)
    {
        // Validate name
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Budget name is required.");
        }

        // Parse period type
        if (!Enum.TryParse<BudgetPeriodType>(request.PeriodType, true, out var periodType))
        {
            throw new ArgumentException($"Invalid period type: {request.PeriodType}. Valid values are: Monthly, Weekly, Biweekly, Custom");
        }

        // Validate custom period has end date
        if (periodType == BudgetPeriodType.Custom && !request.EndDate.HasValue)
        {
            throw new ArgumentException("Custom period budgets require an end date.");
        }

        // Validate categories exist and belong to user
        if (request.Categories.Any())
        {
            var categoryIds = request.Categories.Select(c => c.CategoryId).Distinct().ToList();
            var userCategories = await _categoryRepository.GetByUserIdAsync(request.UserId);
            var userCategoryIds = userCategories.Select(c => c.Id).ToHashSet();

            var invalidCategoryIds = categoryIds.Where(id => !userCategoryIds.Contains(id)).ToList();
            if (invalidCategoryIds.Any())
            {
                throw new ArgumentException($"Invalid category IDs: {string.Join(", ", invalidCategoryIds)}");
            }
        }

        // Create budget entity
        var budget = new Budget
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            PeriodType = periodType,
            StartDate = EnsureUtc(request.StartDate),
            EndDate = request.EndDate.HasValue ? EnsureUtc(request.EndDate.Value) : null,
            IsRecurring = request.IsRecurring,
            IsActive = true,
            UserId = request.UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Add category allocations
        foreach (var categoryRequest in request.Categories)
        {
            budget.BudgetCategories.Add(new BudgetCategory
            {
                CategoryId = categoryRequest.CategoryId,
                BudgetedAmount = categoryRequest.BudgetedAmount,
                AllowRollover = categoryRequest.AllowRollover,
                CarryOverspend = categoryRequest.CarryOverspend,
                IncludeSubcategories = categoryRequest.IncludeSubcategories,
                Notes = categoryRequest.Notes?.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        // Save the budget
        var createdBudget = await _budgetRepository.CreateBudgetAsync(budget, cancellationToken);

        // Load the budget with categories for calculation
        var loadedBudget = await _budgetRepository.GetBudgetByIdAsync(createdBudget.Id, request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("Failed to load created budget.");

        // Calculate and return progress
        return await _calculationService.CalculateBudgetProgressAsync(loadedBudget, request.UserId, cancellationToken);
    }

    private static DateTime EnsureUtc(DateTime dateTime) => dateTime.Kind switch
    {
        DateTimeKind.Utc => dateTime,
        DateTimeKind.Local => dateTime.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
        _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
    };
}
