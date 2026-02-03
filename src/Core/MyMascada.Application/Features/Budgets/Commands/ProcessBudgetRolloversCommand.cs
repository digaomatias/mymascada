using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Budgets.DTOs;
using MyMascada.Application.Features.Budgets.Services;
using MyMascada.Domain.Common;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Budgets.Commands;

/// <summary>
/// Command to process budget rollovers for ended periods.
/// Creates next period budgets for recurring budgets and calculates rollover amounts.
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
    private readonly IBudgetRepository _budgetRepository;
    private readonly IBudgetCalculationService _calculationService;

    public ProcessBudgetRolloversCommandHandler(
        IBudgetRepository budgetRepository,
        IBudgetCalculationService calculationService)
    {
        _budgetRepository = budgetRepository;
        _calculationService = calculationService;
    }

    public async Task<BudgetRolloverResultDto> Handle(ProcessBudgetRolloversCommand request, CancellationToken cancellationToken)
    {
        var result = new BudgetRolloverResultDto
        {
            ProcessedAt = DateTimeProvider.UtcNow,
            PreviewOnly = request.PreviewOnly
        };

        // Get budgets with ended periods that have rollover-enabled categories
        var budgetsNeedingRollover = await _budgetRepository.GetBudgetsNeedingRolloverAsync(
            request.UserId, cancellationToken);

        var budgetList = budgetsNeedingRollover.ToList();

        if (!budgetList.Any())
        {
            result.Message = "No budgets requiring rollover processing.";
            return result;
        }

        foreach (var budget in budgetList)
        {
            var budgetRollover = await ProcessBudgetRolloverAsync(
                budget, request.UserId, request.PreviewOnly, cancellationToken);

            result.ProcessedBudgets.Add(budgetRollover);
        }

        result.TotalBudgetsProcessed = result.ProcessedBudgets.Count;
        result.TotalRolloverAmount = result.ProcessedBudgets
            .SelectMany(b => b.CategoryRollovers)
            .Sum(c => c.RolloverAmount);
        result.NewBudgetsCreated = result.ProcessedBudgets.Count(b => b.NewBudgetCreated);
        result.Message = request.PreviewOnly
            ? $"Preview: {result.TotalBudgetsProcessed} budget(s) would be rolled over."
            : $"Successfully processed {result.TotalBudgetsProcessed} budget rollover(s).";

        return result;
    }

    private async Task<BudgetRolloverDto> ProcessBudgetRolloverAsync(
        Budget budget,
        Guid userId,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        var rolloverDto = new BudgetRolloverDto
        {
            SourceBudgetId = budget.Id,
            SourceBudgetName = budget.Name,
            PeriodStartDate = budget.StartDate,
            PeriodEndDate = budget.GetPeriodEndDate(),
            IsRecurring = budget.IsRecurring
        };

        // Calculate the detailed progress to get actual spending
        var budgetDetail = await _calculationService.CalculateBudgetProgressAsync(
            budget, userId, cancellationToken);

        // Process each category with rollover enabled
        foreach (var budgetCategory in budget.BudgetCategories.Where(bc => !bc.IsDeleted && bc.AllowRollover))
        {
            var categoryProgress = budgetDetail.Categories
                .FirstOrDefault(c => c.CategoryId == budgetCategory.CategoryId);

            if (categoryProgress == null) continue;

            var rolloverAmount = budgetCategory.CalculateNextPeriodRollover(categoryProgress.ActualSpent);

            if (rolloverAmount.HasValue)
            {
                rolloverDto.CategoryRollovers.Add(new CategoryRolloverDto
                {
                    CategoryId = budgetCategory.CategoryId,
                    CategoryName = categoryProgress.CategoryName,
                    BudgetedAmount = budgetCategory.BudgetedAmount,
                    ActualSpent = categoryProgress.ActualSpent,
                    RemainingAmount = categoryProgress.RemainingAmount,
                    RolloverAmount = rolloverAmount.Value,
                    CarryOverspend = budgetCategory.CarryOverspend
                });
            }
        }

        rolloverDto.TotalRollover = rolloverDto.CategoryRollovers.Sum(c => c.RolloverAmount);

        // Create next period budget if recurring and not preview
        if (budget.IsRecurring && !previewOnly && rolloverDto.CategoryRollovers.Any())
        {
            var newBudget = await CreateNextPeriodBudgetAsync(budget, rolloverDto, cancellationToken);
            rolloverDto.NewBudgetId = newBudget.Id;
            rolloverDto.NewBudgetCreated = true;
            rolloverDto.NewPeriodStartDate = newBudget.StartDate;
            rolloverDto.NewPeriodEndDate = newBudget.GetPeriodEndDate();

            // Mark the old budget as inactive
            budget.IsActive = false;
            budget.UpdatedAt = DateTimeProvider.UtcNow;
            await _budgetRepository.UpdateBudgetAsync(budget, cancellationToken);
        }

        return rolloverDto;
    }

    private async Task<Budget> CreateNextPeriodBudgetAsync(
        Budget sourceBudget,
        BudgetRolloverDto rolloverDto,
        CancellationToken cancellationToken)
    {
        // Calculate next period start date
        var periodEnd = sourceBudget.GetPeriodEndDate();
        var nextStartDate = periodEnd.Date.AddDays(1);

        // Create the new budget
        var newBudget = new Budget
        {
            Name = GenerateNextPeriodName(sourceBudget.Name, nextStartDate),
            Description = sourceBudget.Description,
            UserId = sourceBudget.UserId,
            PeriodType = sourceBudget.PeriodType,
            StartDate = nextStartDate,
            EndDate = null, // Let period type calculate the end
            IsRecurring = sourceBudget.IsRecurring,
            IsActive = true,
            CreatedAt = DateTimeProvider.UtcNow,
            UpdatedAt = DateTimeProvider.UtcNow
        };

        // Copy all budget categories from source, applying rollovers
        foreach (var sourceCategory in sourceBudget.BudgetCategories.Where(bc => !bc.IsDeleted))
        {
            var rollover = rolloverDto.CategoryRollovers
                .FirstOrDefault(c => c.CategoryId == sourceCategory.CategoryId);

            newBudget.BudgetCategories.Add(new BudgetCategory
            {
                CategoryId = sourceCategory.CategoryId,
                BudgetedAmount = sourceCategory.BudgetedAmount,
                RolloverAmount = rollover?.RolloverAmount ?? 0,
                AllowRollover = sourceCategory.AllowRollover,
                CarryOverspend = sourceCategory.CarryOverspend,
                IncludeSubcategories = sourceCategory.IncludeSubcategories,
                Notes = sourceCategory.Notes,
                CreatedAt = DateTimeProvider.UtcNow,
                UpdatedAt = DateTimeProvider.UtcNow
            });
        }

        return await _budgetRepository.CreateBudgetAsync(newBudget, cancellationToken);
    }

    private static string GenerateNextPeriodName(string currentName, DateTime nextStartDate)
    {
        // Try to detect date patterns in the name and update them
        // Common patterns: "January 2025", "Jan 2025", "2025-01", "Budget January"

        var monthNames = new[] { "January", "February", "March", "April", "May", "June",
                                  "July", "August", "September", "October", "November", "December" };
        var shortMonthNames = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun",
                                       "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

        var newName = currentName;
        var newMonthName = monthNames[nextStartDate.Month - 1];
        var newShortMonthName = shortMonthNames[nextStartDate.Month - 1];
        var newYear = nextStartDate.Year.ToString();

        // Replace full month names
        foreach (var month in monthNames)
        {
            if (currentName.Contains(month, StringComparison.OrdinalIgnoreCase))
            {
                newName = newName.Replace(month, newMonthName, StringComparison.OrdinalIgnoreCase);
                break;
            }
        }

        // Replace short month names (only if not already replaced)
        if (newName == currentName)
        {
            foreach (var month in shortMonthNames)
            {
                if (currentName.Contains(month, StringComparison.OrdinalIgnoreCase))
                {
                    newName = newName.Replace(month, newShortMonthName, StringComparison.OrdinalIgnoreCase);
                    break;
                }
            }
        }

        // Replace year if present
        var yearPattern = new System.Text.RegularExpressions.Regex(@"\b20\d{2}\b");
        if (yearPattern.IsMatch(newName))
        {
            newName = yearPattern.Replace(newName, newYear);
        }

        // If no date pattern found, append the new period
        if (newName == currentName)
        {
            newName = $"{currentName} - {newMonthName} {newYear}";
        }

        return newName;
    }
}
