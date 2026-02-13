using System.Text;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Common;

namespace MyMascada.Infrastructure.Services.AI;

public class FinancialContextBuilder : IFinancialContextBuilder
{
    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IBudgetRepository _budgetRepository;
    private readonly IRecurringPatternRepository _recurringPatternRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ILogger<FinancialContextBuilder> _logger;

    public FinancialContextBuilder(
        IAccountRepository accountRepository,
        ITransactionRepository transactionRepository,
        IBudgetRepository budgetRepository,
        IRecurringPatternRepository recurringPatternRepository,
        ICategoryRepository categoryRepository,
        ILogger<FinancialContextBuilder> logger)
    {
        _accountRepository = accountRepository;
        _transactionRepository = transactionRepository;
        _budgetRepository = budgetRepository;
        _recurringPatternRepository = recurringPatternRepository;
        _categoryRepository = categoryRepository;
        _logger = logger;
    }

    public async Task<string> BuildContextAsync(Guid userId)
    {
        // Run all data queries in parallel — each wrapped in try/catch so one failure
        // does not crash the whole context build.
        var accountsTask = SafeExecuteAsync(() => _accountRepository.GetByUserIdAsync(userId));
        var categoriesTask = SafeExecuteAsync(() => _categoryRepository.GetByUserIdAsync(userId));
        var budgetTask = SafeExecuteAsync(() => _budgetRepository.GetCurrentBudgetAsync(userId));
        var recurringTask = SafeExecuteAsync(() => _recurringPatternRepository.GetActiveAsync(userId));
        var recentTransactionsTask = SafeExecuteAsync(() => _transactionRepository.GetRecentTransactionsAsync(userId, 15));

        // 12-month transaction data for monthly breakdown
        var now = DateTimeProvider.UtcNow;
        var twelveMonthsAgo = now.AddMonths(-12);
        var monthlyTransactionsTask = SafeExecuteAsync(() =>
            _transactionRepository.GetByDateRangeAsync(userId, twelveMonthsAgo, now));

        await Task.WhenAll(accountsTask, categoriesTask, budgetTask,
            recurringTask, recentTransactionsTask, monthlyTransactionsTask);

        var accounts = await accountsTask;
        var categories = await categoriesTask;
        var budget = await budgetTask;
        var recurringPatterns = await recurringTask;
        var recentTransactions = await recentTransactionsTask;
        var monthlyTransactions = await monthlyTransactionsTask;

        var sb = new StringBuilder();

        BuildAccountsSection(sb, accounts);
        BuildMonthlySummarySection(sb, monthlyTransactions);
        BuildTopSpendingCategoriesSection(sb, monthlyTransactions, categories);
        BuildBudgetSection(sb, budget);
        BuildRecurringExpensesSection(sb, recurringPatterns);
        BuildRecentTransactionsSection(sb, recentTransactions);

        return sb.ToString();
    }

    private static void BuildAccountsSection(
        StringBuilder sb,
        IEnumerable<Domain.Entities.Account>? accounts)
    {
        sb.AppendLine("=== ACCOUNTS ===");

        if (accounts == null || !accounts.Any())
        {
            sb.AppendLine("  No accounts found.");
            sb.AppendLine();
            return;
        }

        decimal total = 0;
        string? currency = null;

        foreach (var account in accounts.Where(a => a.IsActive))
        {
            sb.AppendLine($"  - {account.Name} ({account.Type}): {account.CurrentBalance:N2} {account.Currency}");
            total += account.CurrentBalance;
            currency ??= account.Currency;
        }

        sb.AppendLine($"  Total: {total:N2} {currency ?? "USD"}");
        sb.AppendLine();
    }

    private static void BuildMonthlySummarySection(
        StringBuilder sb,
        IEnumerable<Domain.Entities.Transaction>? transactions)
    {
        sb.AppendLine("=== MONTHLY SUMMARY (Last 12 Months) ===");

        if (transactions == null || !transactions.Any())
        {
            sb.AppendLine("  No transaction data available.");
            sb.AppendLine();
            return;
        }

        var grouped = transactions
            .Where(t => !t.IsExcluded && !t.IsTransfer())
            .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
            .OrderByDescending(g => g.Key.Year)
            .ThenByDescending(g => g.Key.Month);

        foreach (var month in grouped)
        {
            var income = month.Where(t => t.Amount > 0).Sum(t => t.Amount);
            var expenses = month.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount));
            var net = income - expenses;
            var sign = net >= 0 ? "+" : "";

            sb.AppendLine($"  {month.Key.Year}-{month.Key.Month:D2}: Income {income:N2} | Expenses {expenses:N2} | Net {sign}{net:N2}");
        }

        sb.AppendLine();
    }

    private static void BuildTopSpendingCategoriesSection(
        StringBuilder sb,
        IEnumerable<Domain.Entities.Transaction>? transactions,
        IEnumerable<Domain.Entities.Category>? categories)
    {
        sb.AppendLine("=== TOP SPENDING CATEGORIES (This Month) ===");

        if (transactions == null || categories == null)
        {
            sb.AppendLine("  No data available.");
            sb.AppendLine();
            return;
        }

        var now = DateTimeProvider.UtcNow;
        var thisMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var categoryLookup = categories.ToDictionary(c => c.Id, c => c.Name);

        var topCategories = transactions
            .Where(t => t.TransactionDate >= thisMonthStart
                        && t.Amount < 0
                        && !t.IsExcluded
                        && !t.IsTransfer()
                        && t.CategoryId.HasValue)
            .GroupBy(t => t.CategoryId!.Value)
            .Select(g => new
            {
                CategoryId = g.Key,
                Total = g.Sum(t => Math.Abs(t.Amount))
            })
            .OrderByDescending(x => x.Total)
            .Take(10);

        var hasData = false;
        foreach (var cat in topCategories)
        {
            var name = categoryLookup.GetValueOrDefault(cat.CategoryId, $"Category #{cat.CategoryId}");
            sb.AppendLine($"  {name}: {cat.Total:N2}");
            hasData = true;
        }

        if (!hasData)
        {
            sb.AppendLine("  No spending data for this month yet.");
        }

        sb.AppendLine();
    }

    private static void BuildBudgetSection(
        StringBuilder sb,
        Domain.Entities.Budget? budget)
    {
        sb.AppendLine("=== CURRENT BUDGET ===");

        if (budget == null)
        {
            sb.AppendLine("  No active budget configured.");
            sb.AppendLine();
            return;
        }

        sb.AppendLine($"  {budget.Name} | {budget.StartDate:MMMM yyyy} | {budget.GetDaysRemaining()} days remaining");

        if (budget.BudgetCategories.Any())
        {
            foreach (var bc in budget.BudgetCategories.Where(bc => !bc.IsDeleted))
            {
                var categoryName = bc.Category?.Name ?? $"Category #{bc.CategoryId}";
                sb.AppendLine($"  {categoryName}: Budgeted {bc.BudgetedAmount:N2}");
            }

            sb.AppendLine($"  Total Budgeted: {budget.GetTotalBudgetedAmount():N2}");
        }

        sb.AppendLine();
    }

    private static void BuildRecurringExpensesSection(
        StringBuilder sb,
        IEnumerable<Domain.Entities.RecurringPattern>? patterns)
    {
        sb.AppendLine("=== RECURRING EXPENSES (Active) ===");

        if (patterns == null || !patterns.Any())
        {
            sb.AppendLine("  No active recurring expenses detected.");
            sb.AppendLine();
            return;
        }

        decimal totalMonthly = 0;

        foreach (var pattern in patterns.OrderByDescending(p => p.GetMonthlyCost()))
        {
            var monthlyCost = pattern.GetMonthlyCost();
            totalMonthly += monthlyCost;
            sb.AppendLine($"  {pattern.MerchantName}: ~{pattern.AverageAmount:N2}/{pattern.GetIntervalName()} (~{monthlyCost:N2}/month)");
        }

        sb.AppendLine($"  Total monthly recurring: ~{totalMonthly:N2}");
        sb.AppendLine();
    }

    private static void BuildRecentTransactionsSection(
        StringBuilder sb,
        IEnumerable<Domain.Entities.Transaction>? transactions)
    {
        sb.AppendLine("=== RECENT TRANSACTIONS (Last 15) ===");

        if (transactions == null || !transactions.Any())
        {
            sb.AppendLine("  No recent transactions.");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("  Date | Description | Amount | Category");

        foreach (var t in transactions)
        {
            var categoryName = t.Category?.Name ?? "Uncategorized";
            var description = t.GetDisplayDescription();
            sb.AppendLine($"  {t.TransactionDate:yyyy-MM-dd} | {description} | {t.Amount:N2} | {categoryName}");
        }

        sb.AppendLine();
    }

    private async Task<T?> SafeExecuteAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load financial context data for section");
            return default;
        }
    }
}
