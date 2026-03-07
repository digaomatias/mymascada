using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Common;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class BudgetRepository : IBudgetRepository
{
    private readonly ApplicationDbContext _context;

    public BudgetRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    // Budget operations

    public async Task<IEnumerable<Budget>> GetBudgetsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Budgets
            .Include(b => b.BudgetCategories.Where(bc => !bc.IsDeleted))
                .ThenInclude(bc => bc.Category)
            .Where(b => b.UserId == userId && !b.IsDeleted)
            .OrderByDescending(b => b.StartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Budget>> GetActiveBudgetsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Budgets
            .Include(b => b.BudgetCategories.Where(bc => !bc.IsDeleted))
                .ThenInclude(bc => bc.Category)
            .Where(b => b.UserId == userId && b.Status == BudgetStatus.Active && !b.IsDeleted)
            .OrderByDescending(b => b.StartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<Budget?> GetBudgetByIdAsync(int budgetId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Budgets
            .Include(b => b.BudgetCategories.Where(bc => !bc.IsDeleted))
                .ThenInclude(bc => bc.Category)
            .FirstOrDefaultAsync(b => b.Id == budgetId && b.UserId == userId && !b.IsDeleted, cancellationToken);
    }

    public async Task<Budget?> GetBudgetForDateAsync(Guid userId, DateTime date, CancellationToken cancellationToken = default)
    {
        // Get active budgets that could contain the date
        var budgets = await _context.Budgets
            .Include(b => b.BudgetCategories.Where(bc => !bc.IsDeleted))
                .ThenInclude(bc => bc.Category)
            .Where(b => b.UserId == userId
                        && b.Status == BudgetStatus.Active
                        && !b.IsDeleted
                        && b.StartDate <= date)
            .OrderByDescending(b => b.StartDate)
            .ToListAsync(cancellationToken);

        // Find the budget that contains the date
        return budgets.FirstOrDefault(b => b.ContainsDate(date));
    }

    public async Task<Budget?> GetCurrentBudgetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var today = DateTimeProvider.UtcNow;
        return await GetBudgetForDateAsync(userId, today, cancellationToken);
    }

    public async Task<Budget> CreateBudgetAsync(Budget budget, CancellationToken cancellationToken = default)
    {
        budget.CreatedAt = DateTime.UtcNow;
        budget.UpdatedAt = DateTime.UtcNow;

        // Set timestamps for budget categories
        foreach (var bc in budget.BudgetCategories)
        {
            bc.CreatedAt = DateTime.UtcNow;
            bc.UpdatedAt = DateTime.UtcNow;
        }

        _context.Budgets.Add(budget);
        await _context.SaveChangesAsync(cancellationToken);

        // Reload with includes
        return await GetBudgetByIdAsync(budget.Id, budget.UserId, cancellationToken)
               ?? throw new InvalidOperationException("Failed to reload created budget");
    }

    public async Task<Budget> UpdateBudgetAsync(Budget budget, CancellationToken cancellationToken = default)
    {
        budget.UpdatedAt = DateTime.UtcNow;

        _context.Budgets.Update(budget);
        await _context.SaveChangesAsync(cancellationToken);

        // Reload with includes
        return await GetBudgetByIdAsync(budget.Id, budget.UserId, cancellationToken)
               ?? throw new InvalidOperationException("Failed to reload updated budget");
    }

    public async Task DeleteBudgetAsync(int budgetId, Guid userId, CancellationToken cancellationToken = default)
    {
        var budget = await _context.Budgets
            .Include(b => b.BudgetCategories)
            .FirstOrDefaultAsync(b => b.Id == budgetId && b.UserId == userId && !b.IsDeleted, cancellationToken);

        if (budget != null)
        {
            // Soft-delete via Status = Cancelled
            budget.Status = BudgetStatus.Cancelled;
            budget.IsDeleted = true;
            budget.DeletedAt = DateTime.UtcNow;
            budget.UpdatedAt = DateTime.UtcNow;

            // Soft delete all budget categories too
            foreach (var bc in budget.BudgetCategories)
            {
                bc.IsDeleted = true;
                bc.DeletedAt = DateTime.UtcNow;
                bc.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    // Budget Category operations

    public async Task<IEnumerable<BudgetCategory>> GetBudgetCategoriesAsync(int budgetId, CancellationToken cancellationToken = default)
    {
        return await _context.BudgetCategories
            .Include(bc => bc.Category)
            .Where(bc => bc.BudgetId == budgetId && !bc.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public async Task<BudgetCategory?> GetBudgetCategoryAsync(int budgetId, int categoryId, CancellationToken cancellationToken = default)
    {
        return await _context.BudgetCategories
            .Include(bc => bc.Category)
            .FirstOrDefaultAsync(bc => bc.BudgetId == budgetId
                                       && bc.CategoryId == categoryId
                                       && !bc.IsDeleted, cancellationToken);
    }

    public async Task<BudgetCategory> UpsertBudgetCategoryAsync(BudgetCategory budgetCategory, CancellationToken cancellationToken = default)
    {
        var existing = await _context.BudgetCategories
            .FirstOrDefaultAsync(bc => bc.BudgetId == budgetCategory.BudgetId
                                       && bc.CategoryId == budgetCategory.CategoryId
                                       && !bc.IsDeleted, cancellationToken);

        if (existing != null)
        {
            // Update existing
            existing.BudgetedAmount = budgetCategory.BudgetedAmount;
            existing.RolloverAmount = budgetCategory.RolloverAmount;
            existing.AllowRollover = budgetCategory.AllowRollover;
            existing.CarryOverspend = budgetCategory.CarryOverspend;
            existing.IncludeSubcategories = budgetCategory.IncludeSubcategories;
            existing.Notes = budgetCategory.Notes;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            return await GetBudgetCategoryAsync(existing.BudgetId, existing.CategoryId, cancellationToken)
                   ?? existing;
        }
        else
        {
            // Create new
            budgetCategory.CreatedAt = DateTime.UtcNow;
            budgetCategory.UpdatedAt = DateTime.UtcNow;

            _context.BudgetCategories.Add(budgetCategory);
            await _context.SaveChangesAsync(cancellationToken);

            return await GetBudgetCategoryAsync(budgetCategory.BudgetId, budgetCategory.CategoryId, cancellationToken)
                   ?? budgetCategory;
        }
    }

    public async Task RemoveBudgetCategoryAsync(int budgetId, int categoryId, CancellationToken cancellationToken = default)
    {
        var budgetCategory = await _context.BudgetCategories
            .FirstOrDefaultAsync(bc => bc.BudgetId == budgetId
                                       && bc.CategoryId == categoryId
                                       && !bc.IsDeleted, cancellationToken);

        if (budgetCategory != null)
        {
            budgetCategory.IsDeleted = true;
            budgetCategory.DeletedAt = DateTime.UtcNow;
            budgetCategory.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IEnumerable<Budget>> GetBudgetsNeedingRolloverAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await GetExpiredActiveBudgetsAsync(userId, cancellationToken);
    }

    public async Task<IEnumerable<Budget>> GetExpiredActiveBudgetsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var today = DateTimeProvider.UtcNow;

        // Get expired active budgets with their categories, filtering at database level
        return await _context.Budgets
            .Include(b => b.BudgetCategories.Where(bc => !bc.IsDeleted))
                .ThenInclude(bc => bc.Category)
            .Where(b => b.UserId == userId
                        && b.Status == BudgetStatus.Active
                        && !b.IsDeleted
                        && (
                            // Custom periods with explicit EndDate
                            (b.EndDate != null && b.EndDate < today) ||
                            // Recurring periods without EndDate - calculate expiration
                            (b.EndDate == null && (
                                (b.PeriodType == BudgetPeriodType.Weekly && b.StartDate.AddDays(7).AddSeconds(-1) < today) ||
                                (b.PeriodType == BudgetPeriodType.Biweekly && b.StartDate.AddDays(14).AddSeconds(-1) < today) ||
                                (b.PeriodType == BudgetPeriodType.Monthly && b.StartDate.AddMonths(1).AddSeconds(-1) < today)
                            ))
                        ))
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Budget>> GetBudgetsByStatusAsync(Guid userId, BudgetStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.Budgets
            .Include(b => b.BudgetCategories.Where(bc => !bc.IsDeleted))
                .ThenInclude(bc => bc.Category)
            .Where(b => b.UserId == userId && b.Status == status && !b.IsDeleted)
            .OrderByDescending(b => b.StartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Guid>> GetUserIdsWithExpiredActiveBudgetsAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTimeProvider.UtcNow;

        // Get user IDs with expired active budgets, filtering at database level
        return await _context.Budgets
            .Where(b => b.Status == BudgetStatus.Active
                        && !b.IsDeleted
                        && (
                            // Custom periods with explicit EndDate
                            (b.EndDate != null && b.EndDate < today) ||
                            // Recurring periods without EndDate - calculate expiration
                            (b.EndDate == null && (
                                (b.PeriodType == BudgetPeriodType.Weekly && b.StartDate.AddDays(7).AddSeconds(-1) < today) ||
                                (b.PeriodType == BudgetPeriodType.Biweekly && b.StartDate.AddDays(14).AddSeconds(-1) < today) ||
                                (b.PeriodType == BudgetPeriodType.Monthly && b.StartDate.AddMonths(1).AddSeconds(-1) < today)
                            ))
                        ))
            .Select(b => b.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> BudgetNameExistsAsync(Guid userId, string name, int? excludeBudgetId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Budgets
            .Where(b => b.UserId == userId && b.Name == name && !b.IsDeleted);

        if (excludeBudgetId.HasValue)
        {
            query = query.Where(b => b.Id != excludeBudgetId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }
}
