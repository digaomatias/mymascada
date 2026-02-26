using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly ApplicationDbContext _context;

    public CategoryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Category?> GetByIdAsync(int id)
    {
        return await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<IEnumerable<Category>> GetByUserIdAsync(Guid userId)
    {
        return await _context.Categories
            .Include(c => c.ParentCategory)
            .Where(c => c.UserId == userId && c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Category>> GetSystemCategoriesAsync()
    {
        return await _context.Categories
            .Include(c => c.ParentCategory)
            .Where(c => c.IsSystemCategory && c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<Category?> GetByNameAsync(string name, Guid userId, bool includeInactive = false)
    {
        var normalizedName = name.Trim().ToLowerInvariant();
        var query = _context.Categories
            .Where(c => c.UserId == userId && c.Name.ToLower() == normalizedName && !c.IsDeleted);

        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query.FirstOrDefaultAsync();
    }

    public async Task<Category> AddAsync(Category category)
    {
        await _context.Categories.AddAsync(category);
        await _context.SaveChangesAsync();
        return category;
    }

    public async Task UpdateAsync(Category category)
    {
        _context.Categories.Update(category);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Category category)
    {
        category.IsDeleted = true;
        category.DeletedAt = DateTime.UtcNow;
        _context.Categories.Update(category);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(int id, Guid? userId = null)
    {
        var query = _context.Categories.Where(c => c.Id == id);
        
        if (userId.HasValue)
        {
            query = query.Where(c => c.UserId == userId.Value || c.IsSystemCategory);
        }
        
        return await query.AnyAsync();
    }

    public async Task<IEnumerable<Category>> GetCategoriesWithNullCanonicalKeyAsync()
    {
        return await _context.Categories
            .Where(c => c.CanonicalKey == null && !c.IsDeleted)
            .ToListAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<CategoryWithTransactionCount>> GetCategoriesWithTransactionCountsAsync(
        Guid userId,
        int? accountId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        decimal? minAmount = null,
        decimal? maxAmount = null,
        TransactionStatus? status = null,
        string? searchTerm = null,
        bool? isReviewed = null,
        bool? isExcluded = null,
        bool? includeTransfers = null,
        bool? onlyTransfers = null,
        Guid? transferId = null)
    {
        // Build the query with all filters
        var query = _context.Transactions
            .Include(t => t.Category)
            .Include(t => t.Account)
            .Where(t => t.Account.UserId == userId && 
                       !t.IsDeleted && 
                       !t.Account.IsDeleted &&
                       t.CategoryId.HasValue); // Only transactions with categories

        // Apply filters
        if (accountId.HasValue)
            query = query.Where(t => t.AccountId == accountId.Value);

        if (startDate.HasValue)
            query = query.Where(t => t.TransactionDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(t => t.TransactionDate <= endDate.Value);

        if (minAmount.HasValue)
            query = query.Where(t => Math.Abs(t.Amount) >= minAmount.Value);

        if (maxAmount.HasValue)
            query = query.Where(t => Math.Abs(t.Amount) <= maxAmount.Value);

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
            query = query.Where(t => t.Description.Contains(searchTerm) || 
                                   (t.UserDescription != null && t.UserDescription.Contains(searchTerm)));

        if (isReviewed.HasValue)
            query = query.Where(t => t.IsReviewed == isReviewed.Value);

        if (isExcluded.HasValue)
            query = query.Where(t => t.IsExcluded == isExcluded.Value);

        // Handle transfer filters
        if (onlyTransfers == true)
            query = query.Where(t => t.TransferId.HasValue);
        else if (includeTransfers == false)
            query = query.Where(t => !t.TransferId.HasValue);

        if (transferId.HasValue)
            query = query.Where(t => t.TransferId == transferId.Value);

        // Group by category and get counts
        var categoryGroups = await query
            .GroupBy(t => new {
                t.CategoryId,
                t.Category!.Name,
                t.Category.ParentCategoryId,
                t.Category.Color,
                t.Category.Icon,
                t.Category.IsSystemCategory
            })
            .Select(g => new {
                CategoryId = g.Key.CategoryId!.Value,
                Name = g.Key.Name,
                ParentCategoryId = g.Key.ParentCategoryId,
                Color = g.Key.Color,
                Icon = g.Key.Icon,
                IsSystemCategory = g.Key.IsSystemCategory,
                TransactionCount = g.Count()
            })
            .ToListAsync();

        // Get parent category names for full path construction
        var parentCategoryIds = categoryGroups
            .Where(c => c.ParentCategoryId.HasValue)
            .Select(c => c.ParentCategoryId!.Value)
            .Distinct()
            .ToList();

        var parentCategories = await _context.Categories
            .Where(c => parentCategoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name);

        // Convert to domain objects
        var result = categoryGroups.Select(g => new CategoryWithTransactionCount
        {
            Id = g.CategoryId,
            Name = g.Name,
            ParentCategoryId = g.ParentCategoryId,
            Color = g.Color,
            Icon = g.Icon,
            IsSystemCategory = g.IsSystemCategory,
            TransactionCount = g.TransactionCount,
            ParentCategoryName = g.ParentCategoryId.HasValue && parentCategories.ContainsKey(g.ParentCategoryId.Value) 
                ? parentCategories[g.ParentCategoryId.Value] : null,
            FullPath = g.ParentCategoryId.HasValue && parentCategories.ContainsKey(g.ParentCategoryId.Value)
                ? $"{parentCategories[g.ParentCategoryId.Value]} > {g.Name}"
                : g.Name,
            DisplayName = $"{g.Name} ({g.TransactionCount})"
        }).OrderByDescending(c => c.TransactionCount)
          .ThenBy(c => c.Name)
          .ToList();

        return result;
    }
}