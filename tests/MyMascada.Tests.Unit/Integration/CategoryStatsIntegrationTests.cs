using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Transactions.Queries;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Data;
using MyMascada.Infrastructure.Repositories;
using NSubstitute;
using Xunit;

namespace MyMascada.Tests.Unit.Integration;

/// <summary>
/// Integration tests that simulate exactly what the frontend does for category statistics
/// These tests help identify issues with the category stats calculation workflow
/// </summary>
public class CategoryStatsIntegrationTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly TransactionRepository _repository;
    private readonly Guid _userId = Guid.NewGuid();

    public CategoryStatsIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);

        // Create a mock IAccountAccessService that grants full access (Phase 0 behavior)
        var accountAccess = Substitute.For<IAccountAccessService>();
        accountAccess.GetAccessibleAccountIdsAsync(Arg.Any<Guid>())
            .Returns(callInfo =>
            {
                var ids = _context.Accounts
                    .Where(a => a.UserId == callInfo.Arg<Guid>() && !a.IsDeleted)
                    .Select(a => a.Id)
                    .ToHashSet();
                return Task.FromResult<IReadOnlySet<int>>(ids);
            });
        accountAccess.CanAccessAccountAsync(Arg.Any<Guid>(), Arg.Any<int>())
            .Returns(callInfo =>
            {
                var userId = callInfo.ArgAt<Guid>(0);
                var accountId = callInfo.ArgAt<int>(1);
                return Task.FromResult(_context.Accounts.Any(a => a.Id == accountId && a.UserId == userId && !a.IsDeleted));
            });
        accountAccess.IsOwnerAsync(Arg.Any<Guid>(), Arg.Any<int>())
            .Returns(callInfo =>
            {
                var userId = callInfo.ArgAt<Guid>(0);
                var accountId = callInfo.ArgAt<int>(1);
                return Task.FromResult(_context.Accounts.Any(a => a.Id == accountId && a.UserId == userId && !a.IsDeleted));
            });

        var queryService = new MyMascada.Infrastructure.Services.TransactionQueryService(_context, accountAccess);
        _repository = new TransactionRepository(_context, queryService, accountAccess);

        // Seed realistic test data
        SeedRealisticData().Wait();
    }

    private async Task SeedRealisticData()
    {
        // Create user
        var user = new User
        {
            Id = _userId,
            Email = "testuser@example.com",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User"
        };
        _context.Users.Add(user);

        // Create categories
        var groceriesCategory = new Category
        {
            Id = 1,
            Name = "Groceries",
            Type = CategoryType.Expense,
            Color = "#FF5722",
            IsSystemCategory = false,
            IsActive = true,
            SortOrder = 1,
            UserId = _userId
        };

        var restaurantsCategory = new Category
        {
            Id = 2,
            Name = "Restaurants",
            Type = CategoryType.Expense,
            Color = "#FFC107",
            IsSystemCategory = false,
            IsActive = true,
            SortOrder = 2,
            UserId = _userId
        };

        _context.Categories.AddRange(groceriesCategory, restaurantsCategory);

        // Create account
        var checkingAccount = new Account
        {
            Id = 1,
            Name = "Main Checking",
            Type = AccountType.Checking,
            CurrentBalance = 2500.00m,
            Currency = "USD",
            IsActive = true,
            UserId = _userId
        };
        _context.Accounts.Add(checkingAccount);

        // Create realistic transactions over the past few months
        var baseDate = new DateTime(2024, 7, 5, 0, 0, 0, DateTimeKind.Utc); // July 5, 2024 (today)

        var transactions = new List<Transaction>
        {
            // Recent transactions (this month - July 2024)
            new Transaction
            {
                Id = 1,
                Amount = -85.50m,
                TransactionDate = baseDate.AddDays(-2), // July 3
                Description = "Whole Foods Market",
                AccountId = 1,
                CategoryId = 1, // Groceries
                Status = TransactionStatus.Cleared,
                IsReviewed = true
            },
            new Transaction
            {
                Id = 2,
                Amount = -42.75m,
                TransactionDate = baseDate.AddDays(-1), // July 4
                Description = "Local Restaurant",
                AccountId = 1,
                CategoryId = 2, // Restaurants
                Status = TransactionStatus.Cleared,
                IsReviewed = true
            },
            new Transaction
            {
                Id = 3,
                Amount = -120.25m,
                TransactionDate = baseDate, // July 5
                Description = "Costco Wholesale",
                AccountId = 1,
                CategoryId = 1, // Groceries
                Status = TransactionStatus.Cleared,
                IsReviewed = true
            },

            // Last 7 days (June 29 - July 5)
            new Transaction
            {
                Id = 4,
                Amount = -67.30m,
                TransactionDate = baseDate.AddDays(-6), // June 29
                Description = "Safeway",
                AccountId = 1,
                CategoryId = 1, // Groceries
                Status = TransactionStatus.Cleared,
                IsReviewed = true
            },
            new Transaction
            {
                Id = 5,
                Amount = -28.90m,
                TransactionDate = baseDate.AddDays(-4), // July 1
                Description = "Coffee Shop",
                AccountId = 1,
                CategoryId = 2, // Restaurants
                Status = TransactionStatus.Cleared,
                IsReviewed = true
            },

            // Last 30 days but not this month (June 2024)
            new Transaction
            {
                Id = 6,
                Amount = -95.75m,
                TransactionDate = baseDate.AddDays(-15), // June 20
                Description = "Trader Joe's",
                AccountId = 1,
                CategoryId = 1, // Groceries
                Status = TransactionStatus.Cleared,
                IsReviewed = true
            },
            new Transaction
            {
                Id = 7,
                Amount = -45.60m,
                TransactionDate = baseDate.AddDays(-25), // June 10
                Description = "Pizza Place",
                AccountId = 1,
                CategoryId = 2, // Restaurants
                Status = TransactionStatus.Cleared,
                IsReviewed = true
            },

            // Older transactions (3+ months ago)
            new Transaction
            {
                Id = 8,
                Amount = -78.40m,
                TransactionDate = baseDate.AddDays(-95), // April 1
                Description = "Grocery Outlet",
                AccountId = 1,
                CategoryId = 1, // Groceries
                Status = TransactionStatus.Cleared,
                IsReviewed = true
            },
            new Transaction
            {
                Id = 9,
                Amount = -55.20m,
                TransactionDate = baseDate.AddDays(-120), // March 7
                Description = "Fine Dining",
                AccountId = 1,
                CategoryId = 2, // Restaurants
                Status = TransactionStatus.Cleared,
                IsReviewed = true
            }
        };

        _context.Transactions.AddRange(transactions);
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task Frontend_CategoryStats_ThisMonth_SimulatesExactWorkflow()
    {
        // Arrange - Simulate what frontend does for "This Month" filter
        var today = new DateTime(2024, 7, 5, 0, 0, 0, DateTimeKind.Utc);
        var startOfMonth = new DateTime(2024, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        
        var query = new GetTransactionsQuery
        {
            UserId = _userId,
            CategoryId = 1, // Groceries category
            StartDate = startOfMonth,
            EndDate = today,
            PageSize = 1000 // Frontend uses large page size for stats calculation
        };

        // Act - Get transactions exactly like frontend does
        var (transactions, totalCount) = await _repository.GetFilteredAsync(query);
        var transactionList = transactions.ToList();

        // Calculate stats exactly like frontend does
        // For expense categories, we want to show absolute amounts (positive numbers)
        var stats = new
        {
            TransactionCount = transactionList.Count,
            TotalAmount = Math.Abs(transactionList.Sum(t => t.Amount)), // Show absolute value for expense totals
            AverageAmount = transactionList.Count > 0 ? Math.Abs(transactionList.Sum(t => t.Amount)) / transactionList.Count : 0,
            LastTransactionDate = transactionList.Count > 0 ? transactionList.FirstOrDefault()?.TransactionDate : null
        };

        // Assert - Verify the stats are correct for "This Month"
        Assert.Equal(2, stats.TransactionCount); // July 3 and July 5 transactions
        Assert.Equal(205.75m, stats.TotalAmount); // Absolute value: |-85.50 + -120.25| = 205.75
        Assert.Equal(102.875m, stats.AverageAmount); // Absolute average: 205.75 / 2
        Assert.NotNull(stats.LastTransactionDate);

        // Verify the correct transactions are included
        var expectedTransactionIds = new[] { 1, 3 }; // July 3 and July 5
        Assert.All(expectedTransactionIds, id => 
            Assert.Contains(transactionList, t => t.Id == id));
    }

    [Fact]
    public async Task Frontend_CategoryStats_Last7Days_SimulatesExactWorkflow()
    {
        // Arrange - Simulate what frontend does for "Last 7 Days" filter
        var today = new DateTime(2024, 7, 5, 0, 0, 0, DateTimeKind.Utc);
        var sevenDaysAgo = today.AddDays(-6); // June 29
        
        var query = new GetTransactionsQuery
        {
            UserId = _userId,
            CategoryId = 1, // Groceries category
            StartDate = sevenDaysAgo,
            EndDate = today,
            PageSize = 1000
        };

        // Act
        var (transactions, totalCount) = await _repository.GetFilteredAsync(query);
        var transactionList = transactions.ToList();

        var stats = new
        {
            TransactionCount = transactionList.Count,
            TotalAmount = Math.Abs(transactionList.Sum(t => t.Amount)), // Show absolute value for expense totals
            AverageAmount = transactionList.Count > 0 ? Math.Abs(transactionList.Sum(t => t.Amount)) / transactionList.Count : 0
        };

        // Assert - Last 7 days should include June 29, July 3, and July 5
        Assert.Equal(3, stats.TransactionCount); 
        Assert.Equal(273.05m, stats.TotalAmount); // Absolute: |-67.30 + -85.50 + -120.25| = 273.05
        Assert.Equal(91.01666666666667m, stats.AverageAmount, 10); // Absolute average: 273.05 / 3
    }

    [Fact]
    public async Task Frontend_CategoryStats_Last30Days_SimulatesExactWorkflow()
    {
        // Arrange - Simulate what frontend does for "Last 30 Days" filter
        var today = new DateTime(2024, 7, 5, 0, 0, 0, DateTimeKind.Utc);
        var thirtyDaysAgo = today.AddDays(-29); // June 6
        
        var query = new GetTransactionsQuery
        {
            UserId = _userId,
            CategoryId = 1, // Groceries category
            StartDate = thirtyDaysAgo,
            EndDate = today,
            PageSize = 1000
        };

        // Act
        var (transactions, totalCount) = await _repository.GetFilteredAsync(query);
        var transactionList = transactions.ToList();

        var stats = new
        {
            TransactionCount = transactionList.Count,
            TotalAmount = Math.Abs(transactionList.Sum(t => t.Amount)), // Show absolute value for expense totals
            AverageAmount = transactionList.Count > 0 ? Math.Abs(transactionList.Sum(t => t.Amount)) / transactionList.Count : 0
        };

        // Assert - Last 30 days should include June 20, June 29, July 3, July 5
        Assert.Equal(4, stats.TransactionCount);
        Assert.Equal(368.80m, stats.TotalAmount); // Absolute: |-95.75 + -67.30 + -85.50 + -120.25| = 368.80
        Assert.Equal(92.20m, stats.AverageAmount); // Absolute average: 368.80 / 4
    }

    [Fact]
    public async Task Frontend_CategoryStats_AllTime_SimulatesExactWorkflow()
    {
        // Arrange - Simulate what frontend does for "All Time" filter (no date restrictions)
        var query = new GetTransactionsQuery
        {
            UserId = _userId,
            CategoryId = 1, // Groceries category
            PageSize = 1000
        };

        // Act
        var (transactions, totalCount) = await _repository.GetFilteredAsync(query);
        var transactionList = transactions.ToList();

        var stats = new
        {
            TransactionCount = transactionList.Count,
            TotalAmount = Math.Abs(transactionList.Sum(t => t.Amount)), // Show absolute value for expense totals
            AverageAmount = transactionList.Count > 0 ? Math.Abs(transactionList.Sum(t => t.Amount)) / transactionList.Count : 0
        };

        // Assert - All time should include all groceries transactions
        Assert.Equal(5, stats.TransactionCount); // All groceries transactions
        Assert.Equal(447.20m, stats.TotalAmount); // Absolute: Sum of all groceries transactions
        Assert.Equal(89.44m, stats.AverageAmount); // Absolute average: 447.20 / 5
    }

    [Fact]
    public async Task Frontend_CategoryStats_EmptyResults_HandlesGracefully()
    {
        // Arrange - Query a date range with no transactions
        var futureDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
        var query = new GetTransactionsQuery
        {
            UserId = _userId,
            CategoryId = 1, // Groceries category
            StartDate = futureDate,
            EndDate = futureDate.AddDays(30),
            PageSize = 1000
        };

        // Act
        var (transactions, totalCount) = await _repository.GetFilteredAsync(query);
        var transactionList = transactions.ToList();

        var stats = new
        {
            TransactionCount = transactionList.Count,
            TotalAmount = Math.Abs(transactionList.Sum(t => t.Amount)), // Show absolute value for expense totals
            AverageAmount = transactionList.Count > 0 ? Math.Abs(transactionList.Sum(t => t.Amount)) / transactionList.Count : 0
        };

        // Assert - Empty results should be handled gracefully
        Assert.Equal(0, stats.TransactionCount);
        Assert.Equal(0m, stats.TotalAmount);
        Assert.Equal(0m, stats.AverageAmount);
    }

    [Theory]
    [InlineData(1, "Groceries", 447.20)] // All groceries transactions (absolute value)
    [InlineData(2, "Restaurants", 172.45)] // All restaurant transactions (absolute value)
    public async Task Frontend_CategoryStats_DifferentCategories_CalculateCorrectly(int categoryId, string categoryName, decimal expectedTotal)
    {
        // Arrange
        var query = new GetTransactionsQuery
        {
            UserId = _userId,
            CategoryId = categoryId,
            PageSize = 1000
        };

        // Act
        var (transactions, totalCount) = await _repository.GetFilteredAsync(query);
        var transactionList = transactions.ToList();

        var stats = new
        {
            TransactionCount = transactionList.Count,
            TotalAmount = Math.Abs(transactionList.Sum(t => t.Amount)) // Show absolute value for expense totals
        };

        // Assert
        Assert.Equal(expectedTotal, stats.TotalAmount);
        Assert.True(stats.TransactionCount > 0, $"Category {categoryName} should have transactions");
        Assert.All(transactionList, t => Assert.Equal(categoryId, t.CategoryId));
    }

    [Fact]
    public async Task Frontend_CategoryStats_ShowsAbsoluteAmounts_ForExpenseCategories()
    {
        // This test verifies that expense categories show positive amounts (absolute values) to users
        // Even though expenses are stored as negative in the database
        
        // Arrange - Get groceries transactions (expense category)
        var query = new GetTransactionsQuery
        {
            UserId = _userId,
            CategoryId = 1, // Groceries (expense category)
            PageSize = 1000
        };

        // Act
        var (transactions, totalCount) = await _repository.GetFilteredAsync(query);
        var transactionList = transactions.ToList();

        // Calculate stats with raw values (as stored in DB)
        var rawTotalAmount = transactionList.Sum(t => t.Amount);
        
        // Calculate stats with absolute values (as shown to user)
        var displayTotalAmount = Math.Abs(transactionList.Sum(t => t.Amount));
        var displayAverageAmount = transactionList.Count > 0 ? Math.Abs(transactionList.Sum(t => t.Amount)) / transactionList.Count : 0;

        // Assert
        Assert.True(rawTotalAmount < 0, "Expense transactions should be stored as negative in database");
        Assert.True(displayTotalAmount > 0, "Expense totals should be displayed as positive to users");
        Assert.True(displayAverageAmount > 0, "Expense averages should be displayed as positive to users");
        
        // Verify the absolute conversion works correctly
        Assert.Equal(Math.Abs(rawTotalAmount), displayTotalAmount);
        Assert.Equal(447.20m, displayTotalAmount); // Expected absolute total for groceries
        Assert.Equal(89.44m, displayAverageAmount); // Expected absolute average
    }

    [Fact]
    public async Task Backend_CategoryStats_DateFormatConsistency_MatchesFrontend()
    {
        // This test verifies that the date formatting between frontend and backend is consistent
        
        // Arrange - Use the exact same date format that frontend sends
        var frontendDateString = "2024-07-01"; // Frontend sends dates in YYYY-MM-DD format
        var backendDate = DateTime.Parse(frontendDateString + "T00:00:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind);
        
        var query = new GetTransactionsQuery
        {
            UserId = _userId,
            CategoryId = 1,
            StartDate = backendDate,
            EndDate = new DateTime(2024, 7, 5, 23, 59, 59, DateTimeKind.Utc),
            PageSize = 1000
        };

        // Act
        var (transactions, totalCount) = await _repository.GetFilteredAsync(query);

        // Assert - Should include July transactions
        Assert.True(totalCount > 0, "Should find transactions when using frontend date format");
        Assert.All(transactions, t => Assert.True(t.TransactionDate >= backendDate));
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}