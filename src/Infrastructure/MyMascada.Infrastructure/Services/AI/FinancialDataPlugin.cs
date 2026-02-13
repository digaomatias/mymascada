using System.ComponentModel;
using System.Text;
using Microsoft.SemanticKernel;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Transactions.Queries;
using MyMascada.Domain.Common;

namespace MyMascada.Infrastructure.Services.AI;

public class FinancialDataPlugin
{
    private readonly Guid _userId;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IBudgetRepository _budgetRepository;
    private readonly IRecurringPatternRepository _recurringPatternRepository;

    public FinancialDataPlugin(
        Guid userId,
        ITransactionRepository transactionRepository,
        IAccountRepository accountRepository,
        ICategoryRepository categoryRepository,
        IBudgetRepository budgetRepository,
        IRecurringPatternRepository recurringPatternRepository)
    {
        _userId = userId;
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
        _categoryRepository = categoryRepository;
        _budgetRepository = budgetRepository;
        _recurringPatternRepository = recurringPatternRepository;
    }

    [KernelFunction("GetTransactionsByCategory")]
    [Description("Get transactions for a specific category. Use when user asks about spending in a category like restaurants, groceries, etc.")]
    public async Task<string> GetTransactionsByCategory(
        [Description("Category name to search for")] string categoryName,
        [Description("Number of months to look back (default 3)")] int months = 3)
    {
        try
        {
            var categories = await _categoryRepository.GetByUserIdAsync(_userId);
            var matchedCategory = categories.FirstOrDefault(c =>
                c.Name.Contains(categoryName, StringComparison.OrdinalIgnoreCase));

            if (matchedCategory == null)
            {
                return $"No category found matching '{categoryName}'. Available categories: {string.Join(", ", categories.Select(c => c.Name))}";
            }

            var endDate = DateTimeProvider.UtcNow;
            var startDate = endDate.AddMonths(-months);

            var query = new GetTransactionsQuery
            {
                UserId = _userId,
                CategoryId = matchedCategory.Id,
                StartDate = startDate,
                EndDate = endDate,
                PageSize = 50,
                Page = 1,
                SortBy = "TransactionDate",
                SortDirection = "desc"
            };

            var (transactions, totalCount) = await _transactionRepository.GetFilteredAsync(query);
            var transactionList = transactions.ToList();

            if (!transactionList.Any())
            {
                return $"No transactions found in '{matchedCategory.Name}' for the last {months} months.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Transactions in '{matchedCategory.Name}' (last {months} months) - {totalCount} total:");
            sb.AppendLine();

            var total = 0m;
            foreach (var t in transactionList)
            {
                sb.AppendLine($"  {t.TransactionDate:yyyy-MM-dd} | {t.GetDisplayDescription()} | {t.Amount:N2}");
                total += t.Amount;
            }

            sb.AppendLine();
            sb.AppendLine($"Total: {total:N2} ({transactionList.Count} transactions shown, {totalCount} total)");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving transactions by category: {ex.Message}";
        }
    }

    [KernelFunction("GetTransactionsByDateRange")]
    [Description("Get all transactions within a date range")]
    public async Task<string> GetTransactionsByDateRange(
        [Description("Start date (YYYY-MM-DD)")] string startDate,
        [Description("End date (YYYY-MM-DD)")] string endDate)
    {
        try
        {
            if (!DateTime.TryParse(startDate, out var start))
            {
                return $"Invalid start date format: '{startDate}'. Please use YYYY-MM-DD format.";
            }

            if (!DateTime.TryParse(endDate, out var end))
            {
                return $"Invalid end date format: '{endDate}'. Please use YYYY-MM-DD format.";
            }

            start = DateTimeProvider.ToUtc(start);
            end = DateTimeProvider.ToUtc(end);

            var query = new GetTransactionsQuery
            {
                UserId = _userId,
                StartDate = start,
                EndDate = end,
                PageSize = 100,
                Page = 1,
                SortBy = "TransactionDate",
                SortDirection = "desc"
            };

            var (transactions, totalCount) = await _transactionRepository.GetFilteredAsync(query);
            var transactionList = transactions.ToList();

            if (!transactionList.Any())
            {
                return $"No transactions found between {start:yyyy-MM-dd} and {end:yyyy-MM-dd}.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Transactions from {start:yyyy-MM-dd} to {end:yyyy-MM-dd} ({totalCount} total):");
            sb.AppendLine();

            var income = 0m;
            var expenses = 0m;

            foreach (var t in transactionList)
            {
                var categoryName = t.Category?.Name ?? "Uncategorized";
                sb.AppendLine($"  {t.TransactionDate:yyyy-MM-dd} | {t.GetDisplayDescription()} | {t.Amount:N2} | {categoryName}");

                if (t.Amount > 0)
                    income += t.Amount;
                else
                    expenses += Math.Abs(t.Amount);
            }

            sb.AppendLine();
            sb.AppendLine($"Summary: Income {income:N2} | Expenses {expenses:N2} | Net {income - expenses:N2}");
            sb.AppendLine($"Showing {transactionList.Count} of {totalCount} transactions.");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving transactions by date range: {ex.Message}";
        }
    }

    [KernelFunction("SearchTransactions")]
    [Description("Search transactions by description/merchant name")]
    public async Task<string> SearchTransactions(
        [Description("Search term")] string searchTerm,
        [Description("Max results (default 20)")] int limit = 20)
    {
        try
        {
            var query = new GetTransactionsQuery
            {
                UserId = _userId,
                SearchTerm = searchTerm,
                PageSize = Math.Min(limit, 50),
                Page = 1,
                SortBy = "TransactionDate",
                SortDirection = "desc"
            };

            var (transactions, totalCount) = await _transactionRepository.GetFilteredAsync(query);
            var transactionList = transactions.ToList();

            if (!transactionList.Any())
            {
                return $"No transactions found matching '{searchTerm}'.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Search results for '{searchTerm}' ({totalCount} total matches):");
            sb.AppendLine();

            foreach (var t in transactionList)
            {
                var categoryName = t.Category?.Name ?? "Uncategorized";
                sb.AppendLine($"  {t.TransactionDate:yyyy-MM-dd} | {t.GetDisplayDescription()} | {t.Amount:N2} | {categoryName}");
            }

            sb.AppendLine();
            sb.AppendLine($"Showing {transactionList.Count} of {totalCount} matches.");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error searching transactions: {ex.Message}";
        }
    }

    [KernelFunction("GetCategorySpendingBreakdown")]
    [Description("Get spending breakdown by category for a time period")]
    public async Task<string> GetCategorySpendingBreakdown(
        [Description("Number of months to look back (default 3)")] int months = 3)
    {
        try
        {
            var endDate = DateTimeProvider.UtcNow;
            var startDate = endDate.AddMonths(-months);

            var transactions = await _transactionRepository.GetByDateRangeAsync(_userId, startDate, endDate);
            var transactionList = transactions.ToList();

            if (!transactionList.Any())
            {
                return $"No transactions found in the last {months} months.";
            }

            var categories = await _categoryRepository.GetByUserIdAsync(_userId);
            var categoryLookup = categories.ToDictionary(c => c.Id, c => c.Name);

            var breakdown = transactionList
                .Where(t => t.Amount < 0 && !t.IsExcluded && !t.IsTransfer())
                .GroupBy(t => t.CategoryId ?? 0)
                .Select(g => new
                {
                    CategoryName = g.Key == 0
                        ? "Uncategorized"
                        : categoryLookup.GetValueOrDefault(g.Key, $"Category #{g.Key}"),
                    Total = g.Sum(t => Math.Abs(t.Amount)),
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Total)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"Spending breakdown by category (last {months} months):");
            sb.AppendLine();

            var totalSpending = 0m;
            foreach (var cat in breakdown)
            {
                totalSpending += cat.Total;
                sb.AppendLine($"  {cat.CategoryName}: {cat.Total:N2} ({cat.Count} transactions)");
            }

            sb.AppendLine();
            sb.AppendLine($"Total spending: {totalSpending:N2}");

            // Income summary
            var totalIncome = transactionList
                .Where(t => t.Amount > 0 && !t.IsExcluded && !t.IsTransfer())
                .Sum(t => t.Amount);

            sb.AppendLine($"Total income: {totalIncome:N2}");
            sb.AppendLine($"Net: {totalIncome - totalSpending:N2}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving category spending breakdown: {ex.Message}";
        }
    }

    [KernelFunction("GetRecurringExpenses")]
    [Description("Get all active recurring expenses and subscriptions")]
    public async Task<string> GetRecurringExpenses()
    {
        try
        {
            var patterns = await _recurringPatternRepository.GetActiveAsync(_userId);
            var patternList = patterns.ToList();

            if (!patternList.Any())
            {
                return "No active recurring expenses or subscriptions detected.";
            }

            var sb = new StringBuilder();
            sb.AppendLine("Active recurring expenses and subscriptions:");
            sb.AppendLine();

            var totalMonthly = 0m;

            foreach (var pattern in patternList.OrderByDescending(p => p.GetMonthlyCost()))
            {
                var monthlyCost = pattern.GetMonthlyCost();
                totalMonthly += monthlyCost;
                var daysUntilDue = pattern.GetDaysUntilDue(DateTimeProvider.UtcNow);
                var dueInfo = daysUntilDue >= 0
                    ? $"next due in {daysUntilDue} days"
                    : $"overdue by {Math.Abs(daysUntilDue)} days";

                sb.AppendLine($"  {pattern.MerchantName}:");
                sb.AppendLine($"    Amount: ~{pattern.AverageAmount:N2} / {pattern.GetIntervalName()}");
                sb.AppendLine($"    Monthly cost: ~{monthlyCost:N2}");
                sb.AppendLine($"    Status: {pattern.Status} | {dueInfo}");
                sb.AppendLine($"    Confidence: {pattern.GetConfidenceLevel()} ({pattern.Confidence:P0})");
                sb.AppendLine();
            }

            sb.AppendLine($"Total estimated monthly recurring: ~{totalMonthly:N2}");
            sb.AppendLine($"Total estimated annual recurring: ~{totalMonthly * 12:N2}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving recurring expenses: {ex.Message}";
        }
    }

    [KernelFunction("GetAccountDetails")]
    [Description("Get details for a specific account by name")]
    public async Task<string> GetAccountDetails(
        [Description("Account name to look up")] string accountName)
    {
        try
        {
            var accounts = await _accountRepository.GetByUserIdAsync(_userId);
            var matchedAccount = accounts.FirstOrDefault(a =>
                a.Name.Contains(accountName, StringComparison.OrdinalIgnoreCase));

            if (matchedAccount == null)
            {
                return $"No account found matching '{accountName}'. Available accounts: {string.Join(", ", accounts.Select(a => a.Name))}";
            }

            // Calculate actual balance (initial balance + sum of transactions)
            var balances = await _transactionRepository.GetAccountBalancesAsync(_userId);
            var actualBalance = balances.GetValueOrDefault(matchedAccount.Id, matchedAccount.CurrentBalance);

            var sb = new StringBuilder();
            sb.AppendLine($"Account: {matchedAccount.Name}");
            sb.AppendLine($"  Type: {matchedAccount.Type}");
            sb.AppendLine($"  Balance: {actualBalance:N2} {matchedAccount.Currency}");
            sb.AppendLine($"  Institution: {matchedAccount.Institution ?? "Not specified"}");
            sb.AppendLine($"  Active: {matchedAccount.IsActive}");

            if (matchedAccount.LastFourDigits != null)
            {
                sb.AppendLine($"  Last 4 digits: {matchedAccount.LastFourDigits}");
            }

            // Get recent activity for this account
            var endDate = DateTimeProvider.UtcNow;
            var startDate = endDate.AddMonths(-1);

            var (currentMonth, previousMonth) = await _transactionRepository.GetMonthlySpendingAsync(
                matchedAccount.Id, _userId);

            sb.AppendLine();
            sb.AppendLine($"  This month's spending: {currentMonth:N2}");
            sb.AppendLine($"  Last month's spending: {previousMonth:N2}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving account details: {ex.Message}";
        }
    }

    [KernelFunction("GetBudgetStatus")]
    [Description("Get current budget status with spending vs allocated amounts")]
    public async Task<string> GetBudgetStatus()
    {
        try
        {
            var budget = await _budgetRepository.GetCurrentBudgetAsync(_userId);

            if (budget == null)
            {
                return "No active budget configured for the current period.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Budget: {budget.Name}");
            sb.AppendLine($"  Period: {budget.StartDate:yyyy-MM-dd} to {budget.GetPeriodEndDate():yyyy-MM-dd}");
            sb.AppendLine($"  Days remaining: {budget.GetDaysRemaining()}");
            sb.AppendLine($"  Period elapsed: {budget.GetPeriodElapsedPercentage()}%");
            sb.AppendLine();

            if (budget.BudgetCategories.Any(bc => !bc.IsDeleted))
            {
                sb.AppendLine("  Category allocations:");

                // Get actual spending for the budget period
                var transactions = await _transactionRepository.GetByDateRangeAsync(
                    _userId, budget.StartDate, budget.GetPeriodEndDate());

                var spendingByCategory = transactions
                    .Where(t => t.Amount < 0 && !t.IsExcluded && !t.IsTransfer())
                    .GroupBy(t => t.CategoryId ?? 0)
                    .ToDictionary(g => g.Key, g => g.Sum(t => Math.Abs(t.Amount)));

                foreach (var bc in budget.BudgetCategories.Where(bc => !bc.IsDeleted))
                {
                    var categoryName = bc.Category?.Name ?? $"Category #{bc.CategoryId}";
                    var spent = spendingByCategory.GetValueOrDefault(bc.CategoryId, 0m);
                    var remaining = bc.GetEffectiveBudget() - spent;
                    var usedPercent = bc.GetUsedPercentage(spent);
                    var status = bc.IsOverBudget(spent) ? "OVER BUDGET" : "OK";

                    sb.AppendLine($"    {categoryName}: Spent {spent:N2} / Budgeted {bc.GetEffectiveBudget():N2} ({usedPercent}%) - {status}");
                }

                var totalBudgeted = budget.GetTotalBudgetedAmount();
                var totalSpent = spendingByCategory.Values.Sum();

                sb.AppendLine();
                sb.AppendLine($"  Total: Spent {totalSpent:N2} / Budgeted {totalBudgeted:N2}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving budget status: {ex.Message}";
        }
    }

    [KernelFunction("GetUncategorizedTransactions")]
    [Description("Get transactions that haven't been categorized yet")]
    public async Task<string> GetUncategorizedTransactions(
        [Description("Max results (default 20)")] int limit = 20)
    {
        try
        {
            var query = new GetTransactionsQuery
            {
                UserId = _userId,
                NeedsCategorization = true,
                PageSize = Math.Min(limit, 50),
                Page = 1,
                SortBy = "TransactionDate",
                SortDirection = "desc"
            };

            var (transactions, totalCount) = await _transactionRepository.GetFilteredAsync(query);
            var transactionList = transactions.ToList();

            if (!transactionList.Any())
            {
                return "All transactions are categorized. Great job!";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Uncategorized transactions ({totalCount} total):");
            sb.AppendLine();

            foreach (var t in transactionList)
            {
                sb.AppendLine($"  {t.TransactionDate:yyyy-MM-dd} | {t.GetDisplayDescription()} | {t.Amount:N2}");
            }

            sb.AppendLine();
            sb.AppendLine($"Showing {transactionList.Count} of {totalCount} uncategorized transactions.");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving uncategorized transactions: {ex.Message}";
        }
    }
}
