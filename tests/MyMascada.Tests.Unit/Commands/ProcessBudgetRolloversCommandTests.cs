using MyMascada.Domain.Enums;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Budgets.Commands;
using MyMascada.Application.Features.Budgets.DTOs;
using MyMascada.Application.Features.Budgets.Services;
using MyMascada.Domain.Entities;
using NSubstitute;

namespace MyMascada.Tests.Unit.Commands;

public class ProcessBudgetRolloversCommandTests
{
    private readonly IBudgetRepository _budgetRepository;
    private readonly IBudgetCalculationService _calculationService;
    private readonly ProcessBudgetRolloversCommandHandler _handler;
    private readonly Guid _userId;

    public ProcessBudgetRolloversCommandTests()
    {
        _budgetRepository = Substitute.For<IBudgetRepository>();
        _calculationService = Substitute.For<IBudgetCalculationService>();
        _handler = new ProcessBudgetRolloversCommandHandler(_budgetRepository, _calculationService);
        _userId = Guid.NewGuid();
    }

    #region No Budgets Needing Rollover

    [Fact]
    public async Task Handle_NoBudgetsNeedingRollover_ShouldReturnEmptyResult()
    {
        // Arrange
        var command = new ProcessBudgetRolloversCommand { UserId = _userId };
        _budgetRepository.GetBudgetsNeedingRolloverAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<Budget>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalBudgetsProcessed.Should().Be(0);
        result.NewBudgetsCreated.Should().Be(0);
        result.TotalRolloverAmount.Should().Be(0);
        result.ProcessedBudgets.Should().BeEmpty();
        result.Message.Should().Contain("No budgets");
    }

    #endregion

    #region Preview Mode

    [Fact]
    public async Task Handle_PreviewMode_ShouldNotCreateNewBudgets()
    {
        // Arrange
        var budget = CreateTestBudget(isRecurring: true);
        var command = new ProcessBudgetRolloversCommand
        {
            UserId = _userId,
            PreviewOnly = true
        };

        _budgetRepository.GetBudgetsNeedingRolloverAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new[] { budget });

        SetupCalculationServiceMock(budget, spent: 400m, budgeted: 500m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.PreviewOnly.Should().BeTrue();
        result.TotalBudgetsProcessed.Should().Be(1);
        result.NewBudgetsCreated.Should().Be(0);
        result.ProcessedBudgets[0].NewBudgetCreated.Should().BeFalse();
        result.Message.Should().Contain("Preview");

        // Verify no budget was created
        await _budgetRepository.DidNotReceive().CreateBudgetAsync(Arg.Any<Budget>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PreviewMode_ShouldCalculateCorrectRolloverAmount()
    {
        // Arrange
        var budget = CreateTestBudget(isRecurring: true);
        var groceriesCategory = budget.BudgetCategories.First();
        groceriesCategory.AllowRollover = true;
        groceriesCategory.BudgetedAmount = 500m;

        var command = new ProcessBudgetRolloversCommand
        {
            UserId = _userId,
            PreviewOnly = true
        };

        _budgetRepository.GetBudgetsNeedingRolloverAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new[] { budget });

        // Spent $300 of $500 budget - $200 remaining to roll over
        SetupCalculationServiceMock(budget, spent: 300m, budgeted: 500m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.TotalRolloverAmount.Should().Be(200m);
        result.ProcessedBudgets[0].TotalRollover.Should().Be(200m);
        result.ProcessedBudgets[0].CategoryRollovers.Should().HaveCount(1);
        result.ProcessedBudgets[0].CategoryRollovers[0].RolloverAmount.Should().Be(200m);
        result.ProcessedBudgets[0].CategoryRollovers[0].Status.Should().Be("Surplus");
    }

    #endregion

    #region Actual Rollover Processing

    [Fact]
    public async Task Handle_RecurringBudget_ShouldCreateNewBudgetWithRollover()
    {
        // Arrange
        var budget = CreateTestBudget(isRecurring: true);
        var groceriesCategory = budget.BudgetCategories.First();
        groceriesCategory.AllowRollover = true;
        groceriesCategory.BudgetedAmount = 500m;

        var command = new ProcessBudgetRolloversCommand
        {
            UserId = _userId,
            PreviewOnly = false
        };

        _budgetRepository.GetBudgetsNeedingRolloverAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new[] { budget });

        SetupCalculationServiceMock(budget, spent: 400m, budgeted: 500m);

        // Mock the budget creation to return a new budget with ID
        var newBudget = CreateTestBudget(isRecurring: true);
        newBudget.Id = 999;
        newBudget.StartDate = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        _budgetRepository.CreateBudgetAsync(Arg.Any<Budget>(), Arg.Any<CancellationToken>())
            .Returns(newBudget);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.PreviewOnly.Should().BeFalse();
        result.NewBudgetsCreated.Should().Be(1);
        result.ProcessedBudgets[0].NewBudgetCreated.Should().BeTrue();
        result.ProcessedBudgets[0].NewBudgetId.Should().Be(999);

        // Verify new budget was created
        await _budgetRepository.Received(1).CreateBudgetAsync(
            Arg.Is<Budget>(b => b.IsRecurring == true),
            Arg.Any<CancellationToken>());

        // Verify old budget was marked inactive
        await _budgetRepository.Received(1).UpdateBudgetAsync(
            Arg.Is<Budget>(b => b.IsActive == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NonRecurringBudget_ShouldNotCreateNewBudget()
    {
        // Arrange
        var budget = CreateTestBudget(isRecurring: false);
        var groceriesCategory = budget.BudgetCategories.First();
        groceriesCategory.AllowRollover = true;

        var command = new ProcessBudgetRolloversCommand
        {
            UserId = _userId,
            PreviewOnly = false
        };

        _budgetRepository.GetBudgetsNeedingRolloverAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new[] { budget });

        SetupCalculationServiceMock(budget, spent: 400m, budgeted: 500m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.NewBudgetsCreated.Should().Be(0);
        result.ProcessedBudgets[0].NewBudgetCreated.Should().BeFalse();

        // Verify no budget was created
        await _budgetRepository.DidNotReceive().CreateBudgetAsync(Arg.Any<Budget>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region Rollover Calculations

    [Fact]
    public async Task Handle_UnderBudget_ShouldRollOverPositiveAmount()
    {
        // Arrange
        var budget = CreateTestBudget(isRecurring: true);
        var category = budget.BudgetCategories.First();
        category.AllowRollover = true;
        category.BudgetedAmount = 500m;

        var command = new ProcessBudgetRolloversCommand
        {
            UserId = _userId,
            PreviewOnly = true
        };

        _budgetRepository.GetBudgetsNeedingRolloverAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new[] { budget });

        // Spent $300, $200 remaining
        SetupCalculationServiceMock(budget, spent: 300m, budgeted: 500m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        var categoryRollover = result.ProcessedBudgets[0].CategoryRollovers[0];
        categoryRollover.RolloverAmount.Should().Be(200m);
        categoryRollover.Status.Should().Be("Surplus");
    }

    [Fact]
    public async Task Handle_OverBudgetWithCarryOverspend_ShouldRollOverNegativeAmount()
    {
        // Arrange
        var budget = CreateTestBudget(isRecurring: true);
        var category = budget.BudgetCategories.First();
        category.AllowRollover = true;
        category.CarryOverspend = true;
        category.BudgetedAmount = 500m;

        var command = new ProcessBudgetRolloversCommand
        {
            UserId = _userId,
            PreviewOnly = true
        };

        _budgetRepository.GetBudgetsNeedingRolloverAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new[] { budget });

        // Spent $600, $100 over budget
        SetupCalculationServiceMock(budget, spent: 600m, budgeted: 500m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        var categoryRollover = result.ProcessedBudgets[0].CategoryRollovers[0];
        categoryRollover.RolloverAmount.Should().Be(-100m);
        categoryRollover.Status.Should().Be("Deficit");
    }

    [Fact]
    public async Task Handle_OverBudgetWithoutCarryOverspend_ShouldRollOverZero()
    {
        // Arrange
        var budget = CreateTestBudget(isRecurring: true);
        var category = budget.BudgetCategories.First();
        category.AllowRollover = true;
        category.CarryOverspend = false; // Don't carry overspend
        category.BudgetedAmount = 500m;

        var command = new ProcessBudgetRolloversCommand
        {
            UserId = _userId,
            PreviewOnly = true
        };

        _budgetRepository.GetBudgetsNeedingRolloverAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new[] { budget });

        // Spent $600, $100 over budget
        SetupCalculationServiceMock(budget, spent: 600m, budgeted: 500m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        var categoryRollover = result.ProcessedBudgets[0].CategoryRollovers[0];
        categoryRollover.RolloverAmount.Should().Be(0m);
    }

    [Fact]
    public async Task Handle_MultipleCategoriesWithRollover_ShouldCalculateAll()
    {
        // Arrange
        var budget = CreateTestBudgetWithMultipleCategories();

        var command = new ProcessBudgetRolloversCommand
        {
            UserId = _userId,
            PreviewOnly = true
        };

        _budgetRepository.GetBudgetsNeedingRolloverAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new[] { budget });

        SetupCalculationServiceMockForMultipleCategories(budget);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ProcessedBudgets[0].CategoryRollovers.Should().HaveCount(2);

        // Groceries: $500 budgeted, $400 spent = $100 rollover
        var groceries = result.ProcessedBudgets[0].CategoryRollovers
            .First(c => c.CategoryName == "Groceries");
        groceries.RolloverAmount.Should().Be(100m);

        // Entertainment: $200 budgeted, $250 spent, CarryOverspend = true = -$50 rollover
        var entertainment = result.ProcessedBudgets[0].CategoryRollovers
            .First(c => c.CategoryName == "Entertainment");
        entertainment.RolloverAmount.Should().Be(-50m);

        // Total rollover
        result.TotalRolloverAmount.Should().Be(50m); // 100 - 50
    }

    #endregion

    #region Multiple Budgets

    [Fact]
    public async Task Handle_MultipleBudgets_ShouldProcessAll()
    {
        // Arrange
        var budget1 = CreateTestBudget(isRecurring: true, name: "Budget 1");
        budget1.Id = 1;
        budget1.BudgetCategories.First().AllowRollover = true;

        var budget2 = CreateTestBudget(isRecurring: true, name: "Budget 2");
        budget2.Id = 2;
        budget2.BudgetCategories.First().AllowRollover = true;

        var command = new ProcessBudgetRolloversCommand
        {
            UserId = _userId,
            PreviewOnly = true
        };

        _budgetRepository.GetBudgetsNeedingRolloverAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new[] { budget1, budget2 });

        SetupCalculationServiceMock(budget1, spent: 400m, budgeted: 500m);
        SetupCalculationServiceMock(budget2, spent: 300m, budgeted: 500m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.TotalBudgetsProcessed.Should().Be(2);
        result.ProcessedBudgets.Should().HaveCount(2);
        result.TotalRolloverAmount.Should().Be(300m); // 100 + 200
    }

    #endregion

    #region Helper Methods

    private Budget CreateTestBudget(bool isRecurring = true, string name = "January 2025 Budget")
    {
        var groceriesCategory = new Category
        {
            Id = 1,
            Name = "Groceries",
            UserId = _userId
        };

        var budgetCategory = new BudgetCategory
        {
            Id = 1,
            CategoryId = 1,
            Category = groceriesCategory,
            BudgetedAmount = 500m,
            AllowRollover = false,
            CarryOverspend = false,
            IncludeSubcategories = true
        };

        return new Budget
        {
            Id = 1,
            Name = name,
            UserId = _userId,
            PeriodType = BudgetPeriodType.Monthly,
            StartDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = null, // Monthly will calculate end date
            IsRecurring = isRecurring,
            IsActive = true,
            BudgetCategories = new List<BudgetCategory> { budgetCategory }
        };
    }

    private Budget CreateTestBudgetWithMultipleCategories()
    {
        var groceriesCategory = new Category
        {
            Id = 1,
            Name = "Groceries",
            UserId = _userId
        };

        var entertainmentCategory = new Category
        {
            Id = 2,
            Name = "Entertainment",
            UserId = _userId
        };

        var budgetCategories = new List<BudgetCategory>
        {
            new()
            {
                Id = 1,
                CategoryId = 1,
                Category = groceriesCategory,
                BudgetedAmount = 500m,
                AllowRollover = true,
                CarryOverspend = false,
                IncludeSubcategories = true
            },
            new()
            {
                Id = 2,
                CategoryId = 2,
                Category = entertainmentCategory,
                BudgetedAmount = 200m,
                AllowRollover = true,
                CarryOverspend = true,
                IncludeSubcategories = true
            }
        };

        return new Budget
        {
            Id = 1,
            Name = "January 2025 Budget",
            UserId = _userId,
            PeriodType = BudgetPeriodType.Monthly,
            StartDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            IsRecurring = true,
            IsActive = true,
            BudgetCategories = budgetCategories
        };
    }

    private void SetupCalculationServiceMock(Budget budget, decimal spent, decimal budgeted)
    {
        var categoryProgress = new BudgetCategoryProgressDto
        {
            CategoryId = budget.BudgetCategories.First().CategoryId,
            CategoryName = budget.BudgetCategories.First().Category?.Name ?? "Unknown",
            BudgetedAmount = budgeted,
            ActualSpent = spent,
            RemainingAmount = budgeted - spent,
            UsedPercentage = spent / budgeted * 100
        };

        var budgetDetail = new BudgetDetailDto
        {
            Id = budget.Id,
            Name = budget.Name,
            TotalBudgeted = budgeted,
            TotalSpent = spent,
            Categories = new List<BudgetCategoryProgressDto> { categoryProgress }
        };

        _calculationService.CalculateBudgetProgressAsync(budget, _userId, Arg.Any<CancellationToken>())
            .Returns(budgetDetail);
    }

    private void SetupCalculationServiceMockForMultipleCategories(Budget budget)
    {
        var categoryProgresses = new List<BudgetCategoryProgressDto>
        {
            new()
            {
                CategoryId = 1,
                CategoryName = "Groceries",
                BudgetedAmount = 500m,
                ActualSpent = 400m,
                RemainingAmount = 100m,
                UsedPercentage = 80m
            },
            new()
            {
                CategoryId = 2,
                CategoryName = "Entertainment",
                BudgetedAmount = 200m,
                ActualSpent = 250m,
                RemainingAmount = -50m,
                UsedPercentage = 125m
            }
        };

        var budgetDetail = new BudgetDetailDto
        {
            Id = budget.Id,
            Name = budget.Name,
            TotalBudgeted = 700m,
            TotalSpent = 650m,
            Categories = categoryProgresses
        };

        _calculationService.CalculateBudgetProgressAsync(budget, _userId, Arg.Any<CancellationToken>())
            .Returns(budgetDetail);
    }

    #endregion
}
