using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.RuleSuggestions.Services;
using MyMascada.Application.Features.Transactions.Queries;
using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;

namespace MyMascada.Infrastructure.Services.AI;

public class FinancialDataPlugin
{
    private readonly Guid _userId;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IBudgetRepository _budgetRepository;
    private readonly IRecurringPatternRepository _recurringPatternRepository;
    private readonly ITransferRepository _transferRepository;
    private readonly IRuleSuggestionService _ruleSuggestionService;
    private readonly ICategorizationRuleRepository _categorizationRuleRepository;

    public FinancialDataPlugin(
        Guid userId,
        ITransactionRepository transactionRepository,
        IAccountRepository accountRepository,
        ICategoryRepository categoryRepository,
        IBudgetRepository budgetRepository,
        IRecurringPatternRepository recurringPatternRepository,
        ITransferRepository transferRepository,
        IRuleSuggestionService ruleSuggestionService,
        ICategorizationRuleRepository categorizationRuleRepository)
    {
        _userId = userId;
        _transactionRepository = transactionRepository;
        _accountRepository = accountRepository;
        _categoryRepository = categoryRepository;
        _budgetRepository = budgetRepository;
        _recurringPatternRepository = recurringPatternRepository;
        _transferRepository = transferRepository;
        _ruleSuggestionService = ruleSuggestionService;
        _categorizationRuleRepository = categorizationRuleRepository;
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
                var marker = t.IsTransfer() ? " [Transfer]" : t.IsExcluded ? " [Excluded]" : "";
                sb.AppendLine($"  {t.TransactionDate:yyyy-MM-dd} | {t.GetDisplayDescription()} | {t.Amount:N2}{marker}");
                if (!t.IsExcluded && !t.IsTransfer())
                    total += t.Amount;
            }

            sb.AppendLine();
            sb.AppendLine($"Total (excluding transfers): {total:N2} ({transactionList.Count} transactions shown, {totalCount} total)");

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
                var marker = t.IsTransfer() ? " [Transfer]" : t.IsExcluded ? " [Excluded]" : "";
                sb.AppendLine($"  {t.TransactionDate:yyyy-MM-dd} | {t.GetDisplayDescription()} | {t.Amount:N2} | {categoryName}{marker}");

                // Only count non-transfer, non-excluded transactions in totals
                if (!t.IsExcluded && !t.IsTransfer())
                {
                    if (t.Amount > 0)
                        income += t.Amount;
                    else
                        expenses += Math.Abs(t.Amount);
                }
            }

            sb.AppendLine();
            sb.AppendLine($"Summary (excluding transfers): Income {income:N2} | Expenses {expenses:N2} | Net {income - expenses:N2}");
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
                var marker = t.IsTransfer() ? " [Transfer]" : t.IsExcluded ? " [Excluded]" : "";
                sb.AppendLine($"  {t.TransactionDate:yyyy-MM-dd} | {t.GetDisplayDescription()} | {t.Amount:N2} | {categoryName}{marker}");
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
                sb.AppendLine($"  [ID:{t.Id}] {t.TransactionDate:yyyy-MM-dd} | {t.GetDisplayDescription()} | {t.Amount:N2}");
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

    [KernelFunction("GetCategories")]
    [Description("Get all available categories (user and system) grouped by type. Use when user asks about categories or before categorizing transactions.")]
    public async Task<string> GetCategories()
    {
        try
        {
            var userCategories = await _categoryRepository.GetByUserIdAsync(_userId);
            var systemCategories = await _categoryRepository.GetSystemCategoriesAsync();

            var allCategories = userCategories
                .Concat(systemCategories.Where(sc => !userCategories.Any(uc => uc.Id == sc.Id)))
                .ToList();

            if (!allCategories.Any())
            {
                return "No categories found.";
            }

            var sb = new StringBuilder();
            sb.AppendLine("Available categories:");
            sb.AppendLine();

            var grouped = allCategories
                .GroupBy(c => c.Type)
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                sb.AppendLine($"  {group.Key}:");

                var parents = group.Where(c => c.ParentCategoryId == null).OrderBy(c => c.Name);
                var children = group.Where(c => c.ParentCategoryId != null).ToLookup(c => c.ParentCategoryId);

                foreach (var parent in parents)
                {
                    var systemTag = parent.IsSystemCategory ? " [System]" : "";
                    sb.AppendLine($"    [ID:{parent.Id}] {parent.Name}{systemTag}");

                    foreach (var child in children[parent.Id].OrderBy(c => c.Name))
                    {
                        var childSystemTag = child.IsSystemCategory ? " [System]" : "";
                        sb.AppendLine($"      [ID:{child.Id}] {parent.Name} > {child.Name}{childSystemTag}");
                    }
                }

                // Orphaned children (parent not in this group)
                var parentIds = parents.Select(p => p.Id).ToHashSet();
                foreach (var child in group.Where(c => c.ParentCategoryId != null && !parentIds.Contains(c.ParentCategoryId.Value)).OrderBy(c => c.Name))
                {
                    var systemTag = child.IsSystemCategory ? " [System]" : "";
                    sb.AppendLine($"    [ID:{child.Id}] {child.Name}{systemTag}");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving categories: {ex.Message}";
        }
    }

    [KernelFunction("CategorizeTransactions")]
    [Description("Apply category assignments to transactions. ONLY call this after the user has explicitly confirmed your suggested categorizations. Input is a JSON array of objects with transactionId and categoryId.")]
    public async Task<string> CategorizeTransactions(
        [Description("JSON array of objects: [{\"transactionId\":123,\"categoryId\":5}, ...]. Max 50 items.")] string categorizationJson)
    {
        try
        {
            var items = JsonSerializer.Deserialize<List<CategorizationItem>>(categorizationJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (items == null || items.Count == 0)
            {
                return "No categorization items provided. Please provide a JSON array of {transactionId, categoryId} objects.";
            }

            if (items.Count > 50)
            {
                return "Too many items. Maximum 50 transactions per call.";
            }

            // Validate all transactions exist and belong to user
            var transactionIds = items.Select(i => i.TransactionId).ToList();
            var transactions = (await _transactionRepository.GetTransactionsByIdsAsync(transactionIds, _userId)).ToList();
            var transactionLookup = transactions.ToDictionary(t => t.Id);

            // Validate all categories exist
            var userCategories = await _categoryRepository.GetByUserIdAsync(_userId);
            var systemCategories = await _categoryRepository.GetSystemCategoriesAsync();
            var validCategoryIds = userCategories.Select(c => c.Id)
                .Concat(systemCategories.Select(c => c.Id))
                .ToHashSet();

            var successCount = 0;
            var errors = new List<string>();

            foreach (var item in items)
            {
                if (!transactionLookup.TryGetValue(item.TransactionId, out var transaction))
                {
                    errors.Add($"Transaction {item.TransactionId}: not found or not accessible");
                    continue;
                }

                if (!validCategoryIds.Contains(item.CategoryId))
                {
                    errors.Add($"Transaction {item.TransactionId}: category {item.CategoryId} not found");
                    continue;
                }

                transaction.CategoryId = item.CategoryId;
                transaction.IsReviewed = true;
                transaction.MarkAsAutoCategorized("Chat", 1.0m, $"AiChat-{_userId}");
                await _transactionRepository.UpdateAsync(transaction);
                successCount++;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Categorized {successCount} of {items.Count} transactions successfully.");

            if (errors.Any())
            {
                sb.AppendLine();
                sb.AppendLine("Errors:");
                foreach (var error in errors)
                {
                    sb.AppendLine($"  - {error}");
                }
            }

            return sb.ToString();
        }
        catch (JsonException)
        {
            return "Invalid JSON format. Expected: [{\"transactionId\":123,\"categoryId\":5}, ...]";
        }
        catch (Exception ex)
        {
            return $"Error categorizing transactions: {ex.Message}";
        }
    }

    [KernelFunction("GetTransfers")]
    [Description("Get transfers between accounts with optional filters. Use when user asks about transfers, money moved between accounts, or transfer history.")]
    public async Task<string> GetTransfers(
        [Description("Source account name (optional)")] string? sourceAccountName = null,
        [Description("Destination account name (optional)")] string? destinationAccountName = null,
        [Description("Start date YYYY-MM-DD (optional)")] string? startDate = null,
        [Description("End date YYYY-MM-DD (optional)")] string? endDate = null,
        [Description("Status filter: Pending, Completed, Failed, Cancelled, Reversed (optional)")] string? status = null,
        [Description("Max results (default 10, max 50)")] int count = 10)
    {
        try
        {
            var accounts = await _accountRepository.GetByUserIdAsync(_userId);
            var accountList = accounts.ToList();

            int? sourceAccountId = null;
            int? destinationAccountId = null;

            if (!string.IsNullOrWhiteSpace(sourceAccountName))
            {
                var match = accountList.FirstOrDefault(a =>
                    a.Name.Contains(sourceAccountName, StringComparison.OrdinalIgnoreCase));
                if (match == null)
                    return $"No source account found matching '{sourceAccountName}'. Available accounts: {string.Join(", ", accountList.Select(a => a.Name))}";
                sourceAccountId = match.Id;
            }

            if (!string.IsNullOrWhiteSpace(destinationAccountName))
            {
                var match = accountList.FirstOrDefault(a =>
                    a.Name.Contains(destinationAccountName, StringComparison.OrdinalIgnoreCase));
                if (match == null)
                    return $"No destination account found matching '{destinationAccountName}'. Available accounts: {string.Join(", ", accountList.Select(a => a.Name))}";
                destinationAccountId = match.Id;
            }

            DateTime? start = null, end = null;
            if (!string.IsNullOrWhiteSpace(startDate))
            {
                if (!DateTime.TryParse(startDate, out var s))
                    return $"Invalid start date format: '{startDate}'. Please use YYYY-MM-DD.";
                start = DateTimeProvider.ToUtc(s);
            }
            if (!string.IsNullOrWhiteSpace(endDate))
            {
                if (!DateTime.TryParse(endDate, out var e))
                    return $"Invalid end date format: '{endDate}'. Please use YYYY-MM-DD.";
                end = DateTimeProvider.ToUtc(e);
            }

            TransferStatus? transferStatus = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<TransferStatus>(status, true, out var parsed))
                    return $"Invalid status '{status}'. Valid values: Pending, Completed, Failed, Cancelled, Reversed.";
                transferStatus = parsed;
            }

            var (transfers, totalCount) = await _transferRepository.GetFilteredAsync(
                _userId,
                pageSize: Math.Min(count, 50),
                sourceAccountId: sourceAccountId,
                destinationAccountId: destinationAccountId,
                startDate: start,
                endDate: end,
                status: transferStatus);

            var transferList = transfers.ToList();

            if (!transferList.Any())
            {
                return "No transfers found matching your criteria.";
            }

            var accountLookup = accountList.ToDictionary(a => a.Id, a => a.Name);
            var sb = new StringBuilder();
            sb.AppendLine($"Transfers ({totalCount} total):");
            sb.AppendLine();

            foreach (var t in transferList)
            {
                var sourceName = accountLookup.GetValueOrDefault(t.SourceAccountId, $"Account #{t.SourceAccountId}");
                var destName = accountLookup.GetValueOrDefault(t.DestinationAccountId, $"Account #{t.DestinationAccountId}");
                var line = $"  {t.TransferDate:yyyy-MM-dd} | {sourceName} -> {destName} | {t.Amount:N2} {t.Currency} | {t.Status}";

                if (t.FeeAmount.HasValue && t.FeeAmount.Value > 0)
                    line += $" | Fee: {t.FeeAmount.Value:N2}";
                if (t.IsMultiCurrency() && t.ExchangeRate.HasValue)
                    line += $" | Rate: {t.ExchangeRate.Value:N4}";

                sb.AppendLine(line);
            }

            sb.AppendLine();
            sb.AppendLine($"Showing {transferList.Count} of {totalCount} transfers.");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving transfers: {ex.Message}";
        }
    }

    [KernelFunction("GetUpcomingBills")]
    [Description("Get upcoming bills and recurring payments due soon. Use when user asks about upcoming bills, what's due, or scheduled payments.")]
    public async Task<string> GetUpcomingBills(
        [Description("Number of days ahead to look (default 14, max 90)")] int daysAhead = 14)
    {
        try
        {
            daysAhead = Math.Clamp(daysAhead, 1, 90);
            var now = DateTimeProvider.UtcNow;
            var upcoming = await _recurringPatternRepository.GetUpcomingAsync(_userId, now, now.AddDays(daysAhead));
            var upcomingList = upcoming.ToList();

            if (!upcomingList.Any())
            {
                // Check if there are any active patterns at all
                var activePatterns = await _recurringPatternRepository.GetActiveAsync(_userId);
                var activeCount = activePatterns.Count();

                if (activeCount == 0)
                    return $"No upcoming bills in the next {daysAhead} days. No recurring patterns have been detected yet.";

                return $"No bills due in the next {daysAhead} days. You have {activeCount} active recurring patterns tracked overall.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Upcoming bills (next {daysAhead} days):");
            sb.AppendLine();

            var totalExpected = 0m;

            foreach (var pattern in upcomingList.OrderBy(p => p.GetDaysUntilDue(now)))
            {
                var daysUntil = pattern.GetDaysUntilDue(now);
                var dueDate = now.AddDays(daysUntil);
                totalExpected += pattern.AverageAmount;

                sb.AppendLine($"  Due {dueDate:MMM dd} ({daysUntil} days) | {pattern.MerchantName} | ~{pattern.AverageAmount:N2} | {pattern.GetIntervalName()} | {pattern.GetConfidenceLevel()} confidence");
            }

            sb.AppendLine();
            sb.AppendLine($"Total expected: ~{totalExpected:N2}");

            // Also mention total active patterns
            var allActive = await _recurringPatternRepository.GetActiveAsync(_userId);
            var totalActive = allActive.Count();
            sb.AppendLine($"You have {totalActive} active recurring patterns tracked overall.");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving upcoming bills: {ex.Message}";
        }
    }

    [KernelFunction("GetCategorizationRules")]
    [Description("List existing categorization rules with performance stats. Use when user asks about their categorization rules or automation settings.")]
    public async Task<string> GetCategorizationRules(
        [Description("Only show active rules (default true)")] bool activeOnly = true)
    {
        try
        {
            var rules = activeOnly
                ? await _categorizationRuleRepository.GetActiveRulesForUserAsync(_userId)
                : await _categorizationRuleRepository.GetAllRulesForUserAsync(_userId);

            var ruleList = rules.ToList();

            if (!ruleList.Any())
            {
                return activeOnly
                    ? "No active categorization rules found."
                    : "No categorization rules found.";
            }

            var stats = await _categorizationRuleRepository.GetRuleStatisticsAsync(_userId);

            var sb = new StringBuilder();
            var label = activeOnly ? "Active categorization" : "All categorization";
            sb.AppendLine($"{label} rules ({ruleList.Count} total):");
            sb.AppendLine();

            foreach (var rule in ruleList.OrderByDescending(r => r.MatchCount))
            {
                var categoryName = rule.Category?.Name ?? $"Category #{rule.CategoryId}";
                var aiTag = rule.IsAiGenerated ? " [AI-generated]" : "";

                var matchCount = rule.MatchCount;
                var accuracyPercent = 100;
                if (stats.TryGetValue(rule.Id, out var stat))
                {
                    matchCount = stat.MatchCount;
                    accuracyPercent = (int)Math.Round(stat.AccuracyRate * 100);
                }

                sb.AppendLine($"  [ID:{rule.Id}] \"{rule.Name}\" | {rule.Type} \"{rule.Pattern}\" -> {categoryName} | {matchCount} matches | {accuracyPercent}% accuracy{aiTag}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving categorization rules: {ex.Message}";
        }
    }

    [KernelFunction("GenerateRuleSuggestions")]
    [Description("Generate or retrieve pending categorization rule suggestions based on transaction patterns. Use when user wants to automate categorization or asks for rule suggestions.")]
    public async Task<string> GenerateRuleSuggestions()
    {
        try
        {
            // First check for existing pending suggestions
            var existing = await _ruleSuggestionService.GetSuggestionsAsync(_userId, includeSamples: true);
            var pending = existing.Where(s => s.IsPending).ToList();

            if (!pending.Any())
            {
                // Generate new suggestions
                pending = await _ruleSuggestionService.GenerateSuggestionsAsync(_userId, maxSuggestions: 10, minConfidence: 0.6);
            }

            if (!pending.Any())
            {
                return "No rule suggestions could be generated. This may mean your transactions are already well-categorized or there aren't enough patterns to detect.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Rule suggestions ({pending.Count} found):");
            sb.AppendLine();

            foreach (var suggestion in pending.OrderByDescending(s => s.ConfidenceScore))
            {
                var categoryName = suggestion.SuggestedCategory?.Name ?? $"Category #{suggestion.SuggestedCategoryId}";
                sb.AppendLine($"  [SuggestionID:{suggestion.Id}] \"{suggestion.Pattern}\" -> {categoryName} | {suggestion.GetConfidencePercentage()}% confidence | {suggestion.MatchCount} matches");

                if (suggestion.SampleTransactions.Any())
                {
                    var samples = suggestion.SampleTransactions
                        .OrderBy(s => s.SortOrder)
                        .Take(3)
                        .Select(s => $"\"{s.Description}\"");
                    sb.AppendLine($"    Samples: {string.Join(", ", samples)}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("To accept suggestions, confirm which ones you'd like to apply.");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error generating rule suggestions: {ex.Message}";
        }
    }

    [KernelFunction("AcceptRuleSuggestions")]
    [Description("Accept one or more rule suggestions to create categorization rules. ONLY call this after the user has explicitly confirmed which suggestions to accept.")]
    public async Task<string> AcceptRuleSuggestions(
        [Description("JSON array of suggestion IDs to accept, e.g. [15, 16]. Max 20.")] string suggestionIdsJson)
    {
        try
        {
            var ids = JsonSerializer.Deserialize<List<int>>(suggestionIdsJson);

            if (ids == null || ids.Count == 0)
            {
                return "No suggestion IDs provided. Please provide a JSON array of IDs, e.g. [15, 16].";
            }

            if (ids.Count > 20)
            {
                return "Too many suggestions. Maximum 20 per call.";
            }

            var successCount = 0;
            var errors = new List<string>();

            foreach (var id in ids)
            {
                try
                {
                    await _ruleSuggestionService.AcceptSuggestionAsync(id, _userId);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Suggestion {id}: {ex.Message}");
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Accepted {successCount} of {ids.Count} rule suggestions successfully.");

            if (errors.Any())
            {
                sb.AppendLine();
                sb.AppendLine("Errors:");
                foreach (var error in errors)
                {
                    sb.AppendLine($"  - {error}");
                }
            }

            return sb.ToString();
        }
        catch (JsonException)
        {
            return "Invalid JSON format. Expected a JSON array of IDs, e.g. [15, 16].";
        }
        catch (Exception ex)
        {
            return $"Error accepting rule suggestions: {ex.Message}";
        }
    }

    private class CategorizationItem
    {
        public int TransactionId { get; set; }
        public int CategoryId { get; set; }
    }
}
