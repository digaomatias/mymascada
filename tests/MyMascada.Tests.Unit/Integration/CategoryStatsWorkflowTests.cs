using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Features.Transactions.Queries;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Data;
using MyMascada.Infrastructure.Repositories;
using Xunit;

namespace MyMascada.Tests.Unit.Integration;

/// <summary>
/// End-to-end workflow tests that verify the complete category statistics process
/// from backend filtering to frontend display calculations
/// </summary>
public class CategoryStatsWorkflowTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly TransactionRepository _repository;
    private readonly Guid _userId = Guid.NewGuid();

    public CategoryStatsWorkflowTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        var queryService = new MyMascada.Infrastructure.Services.TransactionQueryService(_context);
        _repository = new TransactionRepository(_context, queryService);

        SeedTestData().Wait();
    }

    private async Task SeedTestData()
    {
        // Create user
        var user = new User
        {
            Id = _userId,
            Email = "user@test.com",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User"
        };
        _context.Users.Add(user);

        // Create expense category (most common case)
        var groceriesCategory = new Category
        {
            Id = 1,
            Name = "Groceries",
            Type = CategoryType.Expense, // Type 2
            Color = "#FF5722",
            IsSystemCategory = false,
            IsActive = true,
            SortOrder = 1,
            UserId = _userId
        };

        // Create income category for comparison
        var salaryCategory = new Category
        {
            Id = 2,
            Name = "Salary",
            Type = CategoryType.Income, // Type 1
            Color = "#4CAF50",
            IsSystemCategory = false,
            IsActive = true,
            SortOrder = 2,
            UserId = _userId
        };

        _context.Categories.AddRange(groceriesCategory, salaryCategory);

        // Create account
        var account = new Account
        {
            Id = 1,
            Name = "Main Account",
            Type = AccountType.Checking,
            CurrentBalance = 1000.00m,
            Currency = "USD",
            IsActive = true,
            UserId = _userId
        };
        _context.Accounts.Add(account);

        // Create transactions with known amounts for easy verification
        var today = new DateTime(2024, 7, 5, 0, 0, 0, DateTimeKind.Utc);

        var transactions = new List<Transaction>
        {
            // Expense transactions (stored as negative)
            new Transaction
            {
                Id = 1,
                Amount = -100.00m, // $100 grocery expense
                TransactionDate = today.AddDays(-1),
                Description = "Grocery Store",
                AccountId = 1,
                CategoryId = 1, // Groceries
                Status = TransactionStatus.Cleared,
                IsReviewed = true
            },
            new Transaction
            {
                Id = 2,
                Amount = -50.00m, // $50 grocery expense
                TransactionDate = today,
                Description = "Local Market",
                AccountId = 1,
                CategoryId = 1, // Groceries
                Status = TransactionStatus.Cleared,
                IsReviewed = true
            },

            // Income transaction (stored as positive)
            new Transaction
            {
                Id = 3,
                Amount = 2000.00m, // $2000 salary income
                TransactionDate = today,
                Description = "Monthly Salary",
                AccountId = 1,
                CategoryId = 2, // Salary
                Status = TransactionStatus.Cleared,
                IsReviewed = true
            }
        };

        _context.Transactions.AddRange(transactions);
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task CategoryStats_ExpenseCategory_ShowsAbsoluteAmounts()
    {
        // Arrange - Simulate frontend date range calculation for "This Month"
        var today = new DateTime(2024, 7, 5, 0, 0, 0, DateTimeKind.Utc);
        var startOfMonth = new DateTime(2024, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        
        var query = new GetTransactionsQuery
        {
            UserId = _userId,
            CategoryId = 1, // Groceries (expense category)
            StartDate = startOfMonth,
            EndDate = today,
            PageSize = 1000
        };

        // Act - Backend: Get filtered transactions
        var (transactions, totalCount) = await _repository.GetFilteredAsync(query);
        var transactionList = transactions.ToList();

        // Frontend: Calculate stats with absolute values (as updated in the implementation)
        var totalAmount = transactionList.Sum(t => t.Amount);
        var displayStats = new
        {
            TransactionCount = transactionList.Count,
            TotalAmount = Math.Abs(totalAmount), // Frontend shows absolute value
            AverageAmount = transactionList.Count > 0 ? Math.Abs(totalAmount) / transactionList.Count : 0,
            CategoryType = 2 // Expense category type
        };

        // Assert - Verify the complete workflow
        Assert.Equal(2, displayStats.TransactionCount); // 2 grocery transactions this month
        Assert.Equal(150.00m, displayStats.TotalAmount); // $150 total spent (absolute value)
        Assert.Equal(75.00m, displayStats.AverageAmount); // $75 average spent (absolute value)
        
        // Verify raw data is negative (as stored)
        Assert.True(totalAmount < 0, "Raw expense amounts should be negative in database");
        Assert.Equal(-150.00m, totalAmount); // Raw total is negative
        
        // Verify display logic
        Assert.True(displayStats.TotalAmount > 0, "Display amount should be positive for better UX");
        Assert.Equal(Math.Abs(totalAmount), displayStats.TotalAmount);
    }

    [Fact]
    public async Task CategoryStats_IncomeCategory_ShowsPositiveAmounts()
    {
        // Arrange - Test income category (amounts should remain positive)
        var query = new GetTransactionsQuery
        {
            UserId = _userId,
            CategoryId = 2, // Salary (income category)
            PageSize = 1000
        };

        // Act
        var (transactions, totalCount) = await _repository.GetFilteredAsync(query);
        var transactionList = transactions.ToList();

        var totalAmount = transactionList.Sum(t => t.Amount);
        var displayStats = new
        {
            TransactionCount = transactionList.Count,
            TotalAmount = Math.Abs(totalAmount), // Frontend uses absolute value consistently
            AverageAmount = transactionList.Count > 0 ? Math.Abs(totalAmount) / transactionList.Count : 0,
            CategoryType = 1 // Income category type
        };

        // Assert - Income amounts are already positive, so abs() doesn't change them
        Assert.Equal(1, displayStats.TransactionCount);
        Assert.Equal(2000.00m, displayStats.TotalAmount);
        Assert.Equal(2000.00m, displayStats.AverageAmount);
        
        // Verify raw data is positive (as stored)
        Assert.True(totalAmount > 0, "Income amounts should be positive in database");
        Assert.Equal(2000.00m, totalAmount);
        
        // Verify display amount matches raw amount for income
        Assert.Equal(totalAmount, displayStats.TotalAmount);
    }

    [Fact]
    public async Task CategoryStats_DateRangeFiltering_WorksCorrectly()
    {
        // Test that date filtering works as expected in the complete workflow
        
        // Arrange - Filter to only today's transactions
        var today = new DateTime(2024, 7, 5, 0, 0, 0, DateTimeKind.Utc);
        
        var query = new GetTransactionsQuery
        {
            UserId = _userId,
            CategoryId = 1, // Groceries
            StartDate = today,
            EndDate = today,
            PageSize = 1000
        };

        // Act
        var (transactions, totalCount) = await _repository.GetFilteredAsync(query);
        var transactionList = transactions.ToList();

        var totalAmount = transactionList.Sum(t => t.Amount);
        var displayStats = new
        {
            TransactionCount = transactionList.Count,
            TotalAmount = Math.Abs(totalAmount)
        };

        // Assert - Should only include today's $50 transaction
        Assert.Equal(1, displayStats.TransactionCount);
        Assert.Equal(50.00m, displayStats.TotalAmount); // Only today's $50 expense
        
        // Verify the correct transaction is included
        var todayTransaction = transactionList.Single();
        Assert.Equal(-50.00m, todayTransaction.Amount); // Raw amount is negative
        Assert.Equal("Local Market", todayTransaction.Description);
    }

    [Fact]
    public async Task CategoryStats_EmptyResults_HandleGracefully()
    {
        // Test empty results handling in the complete workflow
        
        // Arrange - Query future date range with no transactions
        var futureDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
        var query = new GetTransactionsQuery
        {
            UserId = _userId,
            CategoryId = 1, // Groceries
            StartDate = futureDate,
            EndDate = futureDate.AddDays(30),
            PageSize = 1000
        };

        // Act
        var (transactions, totalCount) = await _repository.GetFilteredAsync(query);
        var transactionList = transactions.ToList();

        var totalAmount = transactionList.Sum(t => t.Amount);
        var displayStats = new
        {
            TransactionCount = transactionList.Count,
            TotalAmount = Math.Abs(totalAmount),
            AverageAmount = transactionList.Count > 0 ? Math.Abs(totalAmount) / transactionList.Count : 0
        };

        // Assert - Empty results should be handled gracefully
        Assert.Equal(0, displayStats.TransactionCount);
        Assert.Equal(0.00m, displayStats.TotalAmount);
        Assert.Equal(0.00m, displayStats.AverageAmount);
    }

    [Theory]
    [InlineData("2024-07-01", "2024-07-05", 2, 150.00)] // This month: both transactions
    [InlineData("2024-07-05", "2024-07-05", 1, 50.00)]  // Today only: one transaction
    [InlineData("2024-07-04", "2024-07-04", 1, 100.00)] // Yesterday only: one transaction
    public async Task CategoryStats_VariousDateRanges_CalculateCorrectly(
        string startDateStr, string endDateStr, int expectedCount, decimal expectedTotal)
    {
        // Test various date ranges that the frontend might send
        
        // Arrange
        var startDate = DateTime.Parse(startDateStr + "T00:00:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind);
        var endDate = DateTime.Parse(endDateStr + "T23:59:59Z", null, System.Globalization.DateTimeStyles.RoundtripKind);
        
        var query = new GetTransactionsQuery
        {
            UserId = _userId,
            CategoryId = 1, // Groceries
            StartDate = startDate,
            EndDate = endDate,
            PageSize = 1000
        };

        // Act
        var (transactions, totalCount) = await _repository.GetFilteredAsync(query);
        var transactionList = transactions.ToList();

        var totalAmount = transactionList.Sum(t => t.Amount);
        var displayStats = new
        {
            TransactionCount = transactionList.Count,
            TotalAmount = Math.Abs(totalAmount)
        };

        // Assert
        Assert.Equal(expectedCount, displayStats.TransactionCount);
        Assert.Equal(expectedTotal, displayStats.TotalAmount);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}