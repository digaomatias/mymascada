using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Budgets.DTOs;
using MyMascada.Application.Features.Budgets.Services;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using NSubstitute;

namespace MyMascada.Tests.Unit.Services;

public class BudgetCalculationServiceTests
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly BudgetCalculationService _service;
    private readonly Guid _userId;
    private int _transactionIdCounter;

    public BudgetCalculationServiceTests()
    {
        _transactionRepository = Substitute.For<ITransactionRepository>();
        _categoryRepository = Substitute.For<ICategoryRepository>();
        _service = new BudgetCalculationService(_transactionRepository, _categoryRepository);
        _userId = Guid.NewGuid();
        _transactionIdCounter = 1;
    }

    #region ProjectEndOfPeriodSpending Tests

    [Fact]
    public void ProjectEndOfPeriodSpending_WithValidData_ShouldProjectCorrectly()
    {
        // Arrange - spent $500 in 15 days, project for 30 days
        decimal currentSpent = 500m;
        int daysElapsed = 15;
        int totalDays = 30;

        // Act
        var projected = _service.ProjectEndOfPeriodSpending(currentSpent, daysElapsed, totalDays);

        // Assert
        projected.Should().Be(1000m); // $500 / 15 days * 30 days
    }

    [Fact]
    public void ProjectEndOfPeriodSpending_WithZeroDaysElapsed_ShouldReturnCurrentSpent()
    {
        // Arrange
        decimal currentSpent = 500m;
        int daysElapsed = 0;
        int totalDays = 30;

        // Act
        var projected = _service.ProjectEndOfPeriodSpending(currentSpent, daysElapsed, totalDays);

        // Assert
        projected.Should().Be(500m); // No projection possible
    }

    [Fact]
    public void ProjectEndOfPeriodSpending_WithZeroTotalDays_ShouldReturnCurrentSpent()
    {
        // Arrange
        decimal currentSpent = 500m;
        int daysElapsed = 15;
        int totalDays = 0;

        // Act
        var projected = _service.ProjectEndOfPeriodSpending(currentSpent, daysElapsed, totalDays);

        // Assert
        projected.Should().Be(500m);
    }

    [Fact]
    public void ProjectEndOfPeriodSpending_ShouldRoundToTwoDecimalPlaces()
    {
        // Arrange - creates non-round numbers
        decimal currentSpent = 100m;
        int daysElapsed = 7;
        int totalDays = 31;

        // Act
        var projected = _service.ProjectEndOfPeriodSpending(currentSpent, daysElapsed, totalDays);

        // Assert - $100 / 7 * 31 = 442.857...
        projected.Should().Be(442.86m);
    }

    #endregion

    #region GetCategorySpendingAsync Tests

    [Fact]
    public async Task GetCategorySpendingAsync_WithNoCategory_ShouldReturnZeroSpending()
    {
        // Arrange
        int categoryId = 1;
        var startDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2025, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        _categoryRepository.GetByIdAsync(categoryId).Returns((Category?)null);

        // Act
        var result = await _service.GetCategorySpendingAsync(
            categoryId, _userId, startDate, endDate);

        // Assert
        result.CategoryId.Should().Be(categoryId);
        result.CategoryName.Should().Be("Unknown");
        result.TotalSpent.Should().Be(0);
        result.TransactionCount.Should().Be(0);
    }

    [Fact]
    public async Task GetCategorySpendingAsync_WithTransactions_ShouldSumExpenses()
    {
        // Arrange
        int categoryId = 1;
        var startDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2025, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        var category = new Category
        {
            Id = categoryId,
            Name = "Groceries",
            Color = "#00FF00",
            UserId = _userId,
            Type = CategoryType.Expense
        };

        var transactions = new List<Transaction>
        {
            CreateTransaction(-50m, categoryId, new DateTime(2025, 1, 5)),
            CreateTransaction(-75m, categoryId, new DateTime(2025, 1, 10)),
            CreateTransaction(-25m, categoryId, new DateTime(2025, 1, 15)),
            CreateTransaction(100m, categoryId, new DateTime(2025, 1, 12)), // Income - should be excluded
        };

        _categoryRepository.GetByIdAsync(categoryId).Returns(category);
        _transactionRepository.GetByDateRangeAsync(_userId, startDate, endDate)
            .Returns(transactions);

        // Act
        var result = await _service.GetCategorySpendingAsync(
            categoryId, _userId, startDate, endDate);

        // Assert
        result.CategoryId.Should().Be(categoryId);
        result.CategoryName.Should().Be("Groceries");
        result.CategoryColor.Should().Be("#00FF00");
        result.TotalSpent.Should().Be(150m); // 50 + 75 + 25
        result.TransactionCount.Should().Be(3);
    }

    [Fact]
    public async Task GetCategorySpendingAsync_WithSubcategories_ShouldIncludeDescendants()
    {
        // Arrange
        int parentCategoryId = 1;
        int childCategoryId = 2;
        var startDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2025, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        var childCategory = new Category
        {
            Id = childCategoryId,
            Name = "Fast Food",
            ParentCategoryId = parentCategoryId,
            UserId = _userId,
            Type = CategoryType.Expense
        };

        var parentCategory = new Category
        {
            Id = parentCategoryId,
            Name = "Food",
            UserId = _userId,
            Type = CategoryType.Expense,
            SubCategories = new List<Category> { childCategory }
        };

        var transactions = new List<Transaction>
        {
            CreateTransaction(-100m, parentCategoryId, new DateTime(2025, 1, 5)),
            CreateTransaction(-50m, childCategoryId, new DateTime(2025, 1, 10)),
        };

        _categoryRepository.GetByIdAsync(parentCategoryId).Returns(parentCategory);
        _transactionRepository.GetByDateRangeAsync(_userId, startDate, endDate)
            .Returns(transactions);

        // Act
        var result = await _service.GetCategorySpendingAsync(
            parentCategoryId, _userId, startDate, endDate, includeSubcategories: true);

        // Assert
        result.TotalSpent.Should().Be(150m); // 100 + 50
        result.TransactionCount.Should().Be(2);
        result.IncludedCategoryIds.Should().Contain(parentCategoryId);
        result.IncludedCategoryIds.Should().Contain(childCategoryId);
    }

    [Fact]
    public async Task GetCategorySpendingAsync_WithoutSubcategories_ShouldExcludeDescendants()
    {
        // Arrange
        int parentCategoryId = 1;
        int childCategoryId = 2;
        var startDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2025, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        var childCategory = new Category
        {
            Id = childCategoryId,
            Name = "Fast Food",
            ParentCategoryId = parentCategoryId,
            UserId = _userId,
            Type = CategoryType.Expense
        };

        var parentCategory = new Category
        {
            Id = parentCategoryId,
            Name = "Food",
            UserId = _userId,
            Type = CategoryType.Expense,
            SubCategories = new List<Category> { childCategory }
        };

        var transactions = new List<Transaction>
        {
            CreateTransaction(-100m, parentCategoryId, new DateTime(2025, 1, 5)),
            CreateTransaction(-50m, childCategoryId, new DateTime(2025, 1, 10)),
        };

        _categoryRepository.GetByIdAsync(parentCategoryId).Returns(parentCategory);
        _transactionRepository.GetByDateRangeAsync(_userId, startDate, endDate)
            .Returns(transactions);

        // Act
        var result = await _service.GetCategorySpendingAsync(
            parentCategoryId, _userId, startDate, endDate, includeSubcategories: false);

        // Assert
        result.TotalSpent.Should().Be(100m); // Only parent category
        result.TransactionCount.Should().Be(1);
        result.IncludedCategoryIds.Should().ContainSingle().Which.Should().Be(parentCategoryId);
    }

    [Fact]
    public async Task GetCategorySpendingAsync_ShouldExcludeTransfers()
    {
        // Arrange
        int categoryId = 1;
        var startDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2025, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        var category = new Category
        {
            Id = categoryId,
            Name = "Groceries",
            UserId = _userId,
            Type = CategoryType.Expense
        };

        var transactions = new List<Transaction>
        {
            CreateTransaction(-50m, categoryId, new DateTime(2025, 1, 5)),
            CreateTransaction(-100m, categoryId, new DateTime(2025, 1, 10), transferId: Guid.NewGuid()), // Transfer - excluded
        };

        _categoryRepository.GetByIdAsync(categoryId).Returns(category);
        _transactionRepository.GetByDateRangeAsync(_userId, startDate, endDate)
            .Returns(transactions);

        // Act
        var result = await _service.GetCategorySpendingAsync(
            categoryId, _userId, startDate, endDate);

        // Assert
        result.TotalSpent.Should().Be(50m); // Only non-transfer
        result.TransactionCount.Should().Be(1);
    }

    #endregion

    #region GetCategorySpendingBatchAsync Tests

    [Fact]
    public async Task GetCategorySpendingBatchAsync_WithEmptyList_ShouldReturnEmptyDictionary()
    {
        // Arrange
        var categoryIds = new List<int>();
        var startDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2025, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        // Act
        var result = await _service.GetCategorySpendingBatchAsync(
            categoryIds, _userId, startDate, endDate);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCategorySpendingBatchAsync_WithMultipleCategories_ShouldReturnAllSpending()
    {
        // Arrange
        int groceryId = 1;
        int utilitiesId = 2;
        var startDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2025, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        var categories = new List<Category>
        {
            new() { Id = groceryId, Name = "Groceries", UserId = _userId, Type = CategoryType.Expense },
            new() { Id = utilitiesId, Name = "Utilities", UserId = _userId, Type = CategoryType.Expense }
        };

        var transactions = new List<Transaction>
        {
            CreateTransaction(-100m, groceryId, new DateTime(2025, 1, 5)),
            CreateTransaction(-50m, groceryId, new DateTime(2025, 1, 15)),
            CreateTransaction(-200m, utilitiesId, new DateTime(2025, 1, 10)),
        };

        _categoryRepository.GetByUserIdAsync(_userId).Returns(categories);
        _transactionRepository.GetByDateRangeAsync(_userId, startDate, endDate)
            .Returns(transactions);

        // Act
        var result = await _service.GetCategorySpendingBatchAsync(
            new[] { groceryId, utilitiesId }, _userId, startDate, endDate);

        // Assert
        result.Should().HaveCount(2);
        result[groceryId].TotalSpent.Should().Be(150m);
        result[groceryId].TransactionCount.Should().Be(2);
        result[utilitiesId].TotalSpent.Should().Be(200m);
        result[utilitiesId].TransactionCount.Should().Be(1);
    }

    #endregion

    #region GenerateBudgetSuggestionsAsync Tests

    [Fact]
    public async Task GenerateBudgetSuggestionsAsync_WithNoCategories_ShouldReturnEmptyList()
    {
        // Arrange
        _categoryRepository.GetByUserIdAsync(_userId).Returns(new List<Category>());

        // Act
        var result = await _service.GenerateBudgetSuggestionsAsync(_userId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateBudgetSuggestionsAsync_WithNoTransactions_ShouldReturnEmptyList()
    {
        // Arrange
        var categories = new List<Category>
        {
            new() { Id = 1, Name = "Groceries", UserId = _userId, Type = CategoryType.Expense }
        };

        _categoryRepository.GetByUserIdAsync(_userId).Returns(categories);
        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(new List<Transaction>());

        // Act
        var result = await _service.GenerateBudgetSuggestionsAsync(_userId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateBudgetSuggestionsAsync_WithConsistentSpending_ShouldReturnHighConfidence()
    {
        // Arrange
        int categoryId = 1;
        var categories = new List<Category>
        {
            new() { Id = categoryId, Name = "Groceries", UserId = _userId, Type = CategoryType.Expense, Color = "#00FF00", Icon = "cart" }
        };

        // Consistent spending: $400-$410 per month for 3 months
        var transactions = new List<Transaction>
        {
            // November
            CreateTransaction(-400m, categoryId, new DateTime(2024, 11, 15)),
            // December
            CreateTransaction(-405m, categoryId, new DateTime(2024, 12, 15)),
            // January
            CreateTransaction(-410m, categoryId, new DateTime(2025, 1, 10)),
        };

        _categoryRepository.GetByUserIdAsync(_userId).Returns(categories);
        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GenerateBudgetSuggestionsAsync(_userId, monthsToAnalyze: 3);

        // Assert
        result.Should().HaveCount(1);
        var suggestion = result[0];
        suggestion.CategoryId.Should().Be(categoryId);
        suggestion.CategoryName.Should().Be("Groceries");
        suggestion.CategoryColor.Should().Be("#00FF00");
        suggestion.CategoryIcon.Should().Be("cart");
        suggestion.AverageMonthlySpending.Should().BeApproximately(405m, 0.5m);
        suggestion.Confidence.Should().BeGreaterThan(0.9m); // High confidence for consistent spending
    }

    [Fact]
    public async Task GenerateBudgetSuggestionsAsync_WithVariableSpending_ShouldReturnLowerConfidence()
    {
        // Arrange
        int categoryId = 1;
        var categories = new List<Category>
        {
            new() { Id = categoryId, Name = "Entertainment", UserId = _userId, Type = CategoryType.Expense }
        };

        // Highly variable spending: $100, $500, $50 per month
        var transactions = new List<Transaction>
        {
            CreateTransaction(-100m, categoryId, new DateTime(2024, 11, 15)),
            CreateTransaction(-500m, categoryId, new DateTime(2024, 12, 15)),
            CreateTransaction(-50m, categoryId, new DateTime(2025, 1, 10)),
        };

        _categoryRepository.GetByUserIdAsync(_userId).Returns(categories);
        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GenerateBudgetSuggestionsAsync(_userId, monthsToAnalyze: 3);

        // Assert
        result.Should().HaveCount(1);
        var suggestion = result[0];
        suggestion.Confidence.Should().BeLessThan(0.5m); // Lower confidence for variable spending
    }

    [Fact]
    public async Task GenerateBudgetSuggestionsAsync_ShouldRoundToFriendlyNumbers()
    {
        // Arrange
        int categoryId = 1;
        var categories = new List<Category>
        {
            new() { Id = categoryId, Name = "Groceries", UserId = _userId, Type = CategoryType.Expense }
        };

        // Average will be ~$147
        var transactions = new List<Transaction>
        {
            CreateTransaction(-145m, categoryId, new DateTime(2024, 11, 15)),
            CreateTransaction(-148m, categoryId, new DateTime(2024, 12, 15)),
            CreateTransaction(-149m, categoryId, new DateTime(2025, 1, 10)),
        };

        _categoryRepository.GetByUserIdAsync(_userId).Returns(categories);
        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GenerateBudgetSuggestionsAsync(_userId, monthsToAnalyze: 3);

        // Assert
        result.Should().HaveCount(1);
        // $147 average should round up to $150 (nearest $10 for amounts 100-500)
        result[0].SuggestedBudget.Should().Be(150m);
    }

    [Fact]
    public async Task GenerateBudgetSuggestionsAsync_ShouldExcludeIncomeCategories()
    {
        // Arrange
        var categories = new List<Category>
        {
            new() { Id = 1, Name = "Salary", UserId = _userId, Type = CategoryType.Income },
            new() { Id = 2, Name = "Groceries", UserId = _userId, Type = CategoryType.Expense }
        };

        var transactions = new List<Transaction>
        {
            CreateTransaction(5000m, 1, new DateTime(2025, 1, 1)), // Income
            CreateTransaction(-100m, 2, new DateTime(2025, 1, 15)), // Expense
        };

        _categoryRepository.GetByUserIdAsync(_userId).Returns(categories);
        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GenerateBudgetSuggestionsAsync(_userId, monthsToAnalyze: 3);

        // Assert
        result.Should().ContainSingle();
        result[0].CategoryName.Should().Be("Groceries");
    }

    [Fact]
    public async Task GenerateBudgetSuggestionsAsync_ShouldSortByAverageSpending()
    {
        // Arrange
        var categories = new List<Category>
        {
            new() { Id = 1, Name = "Low", UserId = _userId, Type = CategoryType.Expense },
            new() { Id = 2, Name = "High", UserId = _userId, Type = CategoryType.Expense },
            new() { Id = 3, Name = "Medium", UserId = _userId, Type = CategoryType.Expense }
        };

        var transactions = new List<Transaction>
        {
            CreateTransaction(-50m, 1, new DateTime(2025, 1, 5)),
            CreateTransaction(-500m, 2, new DateTime(2025, 1, 10)),
            CreateTransaction(-200m, 3, new DateTime(2025, 1, 15)),
        };

        _categoryRepository.GetByUserIdAsync(_userId).Returns(categories);
        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GenerateBudgetSuggestionsAsync(_userId, monthsToAnalyze: 3);

        // Assert
        result.Should().HaveCount(3);
        result[0].CategoryName.Should().Be("High");
        result[1].CategoryName.Should().Be("Medium");
        result[2].CategoryName.Should().Be("Low");
    }

    #endregion

    #region CalculateBudgetProgressAsync Tests

    [Fact]
    public async Task CalculateBudgetProgressAsync_WithNoCategories_ShouldReturnZeroTotals()
    {
        // Arrange
        var budget = new Budget
        {
            Id = 1,
            Name = "January Budget",
            UserId = _userId,
            PeriodType = BudgetPeriodType.Monthly,
            StartDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true,
            BudgetCategories = new List<BudgetCategory>()
        };

        // Act
        var result = await _service.CalculateBudgetProgressAsync(budget, _userId);

        // Assert
        result.Id.Should().Be(1);
        result.Name.Should().Be("January Budget");
        result.TotalBudgeted.Should().Be(0);
        result.TotalSpent.Should().Be(0);
        result.TotalRemaining.Should().Be(0);
        result.UsedPercentage.Should().Be(0);
        result.Categories.Should().BeEmpty();
    }

    [Fact]
    public async Task CalculateBudgetProgressAsync_WithCategories_ShouldCalculateProgress()
    {
        // Arrange
        int groceryId = 1;
        var groceryCategory = new Category
        {
            Id = groceryId,
            Name = "Groceries",
            Color = "#00FF00",
            UserId = _userId,
            Type = CategoryType.Expense
        };

        var budget = new Budget
        {
            Id = 1,
            Name = "January Budget",
            UserId = _userId,
            PeriodType = BudgetPeriodType.Monthly,
            StartDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true,
            BudgetCategories = new List<BudgetCategory>
            {
                new()
                {
                    Id = 1,
                    CategoryId = groceryId,
                    Category = groceryCategory,
                    BudgetedAmount = 500m,
                    IncludeSubcategories = true
                }
            }
        };

        var categories = new List<Category> { groceryCategory };
        var transactions = new List<Transaction>
        {
            CreateTransaction(-200m, groceryId, new DateTime(2025, 1, 10)),
        };

        _categoryRepository.GetByUserIdAsync(_userId).Returns(categories);
        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.CalculateBudgetProgressAsync(budget, _userId);

        // Assert
        result.TotalBudgeted.Should().Be(500m);
        result.TotalSpent.Should().Be(200m);
        result.TotalRemaining.Should().Be(300m);
        result.UsedPercentage.Should().Be(40m);
        result.Categories.Should().HaveCount(1);

        var categoryProgress = result.Categories[0];
        categoryProgress.CategoryId.Should().Be(groceryId);
        categoryProgress.CategoryName.Should().Be("Groceries");
        categoryProgress.BudgetedAmount.Should().Be(500m);
        categoryProgress.ActualSpent.Should().Be(200m);
        categoryProgress.RemainingAmount.Should().Be(300m);
        categoryProgress.IsOverBudget.Should().BeFalse();
    }

    [Fact]
    public async Task CalculateBudgetProgressAsync_WithOverspending_ShouldIndicateOverBudget()
    {
        // Arrange
        int categoryId = 1;
        var category = new Category
        {
            Id = categoryId,
            Name = "Entertainment",
            UserId = _userId,
            Type = CategoryType.Expense
        };

        var budget = new Budget
        {
            Id = 1,
            Name = "January Budget",
            UserId = _userId,
            PeriodType = BudgetPeriodType.Monthly,
            StartDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true,
            BudgetCategories = new List<BudgetCategory>
            {
                new()
                {
                    Id = 1,
                    CategoryId = categoryId,
                    Category = category,
                    BudgetedAmount = 100m,
                    IncludeSubcategories = true
                }
            }
        };

        var categories = new List<Category> { category };
        var transactions = new List<Transaction>
        {
            CreateTransaction(-150m, categoryId, new DateTime(2025, 1, 10)), // 150% of budget
        };

        _categoryRepository.GetByUserIdAsync(_userId).Returns(categories);
        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.CalculateBudgetProgressAsync(budget, _userId);

        // Assert
        result.TotalSpent.Should().Be(150m);
        result.TotalRemaining.Should().Be(-50m);
        result.UsedPercentage.Should().Be(150m);

        var categoryProgress = result.Categories[0];
        categoryProgress.IsOverBudget.Should().BeTrue();
        categoryProgress.RemainingAmount.Should().Be(-50m);
    }

    [Fact]
    public async Task CalculateBudgetProgressAsync_ShouldSortByUsedPercentageDescending()
    {
        // Arrange
        var cat1 = new Category { Id = 1, Name = "Low", UserId = _userId, Type = CategoryType.Expense };
        var cat2 = new Category { Id = 2, Name = "High", UserId = _userId, Type = CategoryType.Expense };
        var cat3 = new Category { Id = 3, Name = "Medium", UserId = _userId, Type = CategoryType.Expense };

        var budget = new Budget
        {
            Id = 1,
            Name = "Test Budget",
            UserId = _userId,
            PeriodType = BudgetPeriodType.Monthly,
            StartDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            BudgetCategories = new List<BudgetCategory>
            {
                new() { Id = 1, CategoryId = 1, Category = cat1, BudgetedAmount = 100m, IncludeSubcategories = true },
                new() { Id = 2, CategoryId = 2, Category = cat2, BudgetedAmount = 100m, IncludeSubcategories = true },
                new() { Id = 3, CategoryId = 3, Category = cat3, BudgetedAmount = 100m, IncludeSubcategories = true }
            }
        };

        var categories = new List<Category> { cat1, cat2, cat3 };
        var transactions = new List<Transaction>
        {
            CreateTransaction(-20m, 1, new DateTime(2025, 1, 5)),   // 20%
            CreateTransaction(-90m, 2, new DateTime(2025, 1, 10)),  // 90%
            CreateTransaction(-50m, 3, new DateTime(2025, 1, 15)),  // 50%
        };

        _categoryRepository.GetByUserIdAsync(_userId).Returns(categories);
        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.CalculateBudgetProgressAsync(budget, _userId);

        // Assert
        result.Categories[0].CategoryName.Should().Be("High");
        result.Categories[1].CategoryName.Should().Be("Medium");
        result.Categories[2].CategoryName.Should().Be("Low");
    }

    [Fact]
    public async Task CalculateBudgetProgressAsync_WithRollover_ShouldIncludeInEffectiveBudget()
    {
        // Arrange
        int categoryId = 1;
        var category = new Category
        {
            Id = categoryId,
            Name = "Groceries",
            UserId = _userId,
            Type = CategoryType.Expense
        };

        var budget = new Budget
        {
            Id = 1,
            Name = "January Budget",
            UserId = _userId,
            PeriodType = BudgetPeriodType.Monthly,
            StartDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            BudgetCategories = new List<BudgetCategory>
            {
                new()
                {
                    Id = 1,
                    CategoryId = categoryId,
                    Category = category,
                    BudgetedAmount = 500m,
                    RolloverAmount = 100m, // $100 rolled over from last month
                    AllowRollover = true,
                    IncludeSubcategories = true
                }
            }
        };

        var categories = new List<Category> { category };
        var transactions = new List<Transaction>
        {
            CreateTransaction(-400m, categoryId, new DateTime(2025, 1, 10)),
        };

        _categoryRepository.GetByUserIdAsync(_userId).Returns(categories);
        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.CalculateBudgetProgressAsync(budget, _userId);

        // Assert
        var categoryProgress = result.Categories[0];
        categoryProgress.BudgetedAmount.Should().Be(500m);
        categoryProgress.RolloverAmount.Should().Be(100m);
        categoryProgress.EffectiveBudget.Should().Be(600m); // 500 + 100
        categoryProgress.ActualSpent.Should().Be(400m);
        categoryProgress.RemainingAmount.Should().Be(200m); // 600 - 400
    }

    #endregion

    #region ToBudgetSummaryAsync Tests

    [Fact]
    public async Task ToBudgetSummaryAsync_ShouldReturnCorrectSummary()
    {
        // Arrange
        int categoryId = 1;
        var category = new Category
        {
            Id = categoryId,
            Name = "Groceries",
            UserId = _userId,
            Type = CategoryType.Expense
        };

        var budget = new Budget
        {
            Id = 1,
            Name = "January Budget",
            Description = "Monthly household budget",
            UserId = _userId,
            PeriodType = BudgetPeriodType.Monthly,
            StartDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            IsRecurring = true,
            IsActive = true,
            BudgetCategories = new List<BudgetCategory>
            {
                new()
                {
                    Id = 1,
                    CategoryId = categoryId,
                    Category = category,
                    BudgetedAmount = 500m,
                    IncludeSubcategories = true
                }
            }
        };

        var categories = new List<Category> { category };
        var transactions = new List<Transaction>
        {
            CreateTransaction(-200m, categoryId, new DateTime(2025, 1, 10)),
        };

        _categoryRepository.GetByUserIdAsync(_userId).Returns(categories);
        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.ToBudgetSummaryAsync(budget, _userId);

        // Assert
        result.Id.Should().Be(1);
        result.Name.Should().Be("January Budget");
        result.Description.Should().Be("Monthly household budget");
        result.PeriodType.Should().Be("Monthly");
        result.IsRecurring.Should().BeTrue();
        result.IsActive.Should().BeTrue();
        result.CategoryCount.Should().Be(1);
        result.TotalBudgeted.Should().Be(500m);
        result.TotalSpent.Should().Be(200m);
        result.TotalRemaining.Should().Be(300m);
        result.UsedPercentage.Should().Be(40m);
        // Note: IsCurrentPeriod depends on current date so we don't assert it here
    }

    #endregion

    #region Helper Methods

    private Transaction CreateTransaction(
        decimal amount,
        int? categoryId,
        DateTime date,
        Guid? transferId = null)
    {
        return new Transaction
        {
            Id = _transactionIdCounter++,
            Amount = amount,
            CategoryId = categoryId,
            TransactionDate = date,
            Description = "Test Transaction",
            TransferId = transferId
        };
    }

    #endregion
}
