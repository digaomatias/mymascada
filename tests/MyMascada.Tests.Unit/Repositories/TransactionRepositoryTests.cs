using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Features.Transactions.Queries;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Data;
using MyMascada.Infrastructure.Repositories;
using Xunit;

namespace MyMascada.Tests.Unit.Repositories;

public class TransactionRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly TransactionRepository _repository;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();

    public TransactionRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        var queryService = new MyMascada.Infrastructure.Services.TransactionQueryService(_context);
        _repository = new TransactionRepository(_context, queryService);

        // Seed test data
        SeedTestData().Wait();
    }

    private async Task SeedTestData()
    {
        // Create test users
        var user = new User
        {
            Id = _userId,
            Email = "test@example.com",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User"
        };

        var otherUser = new User
        {
            Id = _otherUserId,
            Email = "other@example.com",
            PasswordHash = "hash",
            FirstName = "Other",
            LastName = "User"
        };

        _context.Users.AddRange(user, otherUser);

        // Create test categories
        var expenseCategory = new Category
        {
            Id = 1,
            Name = "Groceries",
            Type = CategoryType.Expense,
            IsSystemCategory = false,
            IsActive = true,
            SortOrder = 1,
            UserId = _userId
        };

        var incomeCategory = new Category
        {
            Id = 2,
            Name = "Salary",
            Type = CategoryType.Income,
            IsSystemCategory = false,
            IsActive = true,
            SortOrder = 2,
            UserId = _userId
        };

        _context.Categories.AddRange(expenseCategory, incomeCategory);

        // Create test accounts
        var checkingAccount = new Account
        {
            Id = 1,
            Name = "Checking Account",
            Type = AccountType.Checking,
            CurrentBalance = 1000m,
            Currency = "USD",
            IsActive = true,
            UserId = _userId
        };

        var savingsAccount = new Account
        {
            Id = 2,
            Name = "Savings Account",
            Type = AccountType.Savings,
            CurrentBalance = 5000m,
            Currency = "USD",
            IsActive = true,
            UserId = _userId
        };

        var otherUserAccount = new Account
        {
            Id = 3,
            Name = "Other User Account",
            Type = AccountType.Checking,
            CurrentBalance = 2000m,
            Currency = "USD",
            IsActive = true,
            UserId = _otherUserId
        };

        _context.Accounts.AddRange(checkingAccount, savingsAccount, otherUserAccount);

        // Create test transactions with various dates
        var baseDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        var transactions = new List<Transaction>
        {
            // Groceries category transactions
            new Transaction
            {
                Id = 1,
                Amount = -100.50m,
                TransactionDate = baseDate.AddDays(-10), // Jan 5, 2024
                Description = "Grocery Store",
                AccountId = 1,
                CategoryId = 1,
                Status = TransactionStatus.Cleared,
                IsReviewed = true
            },
            new Transaction
            {
                Id = 2,
                Amount = -75.25m,
                TransactionDate = baseDate.AddDays(-5), // Jan 10, 2024
                Description = "Supermarket",
                AccountId = 1,
                CategoryId = 1,
                Status = TransactionStatus.Cleared,
                IsReviewed = true
            },
            new Transaction
            {
                Id = 3,
                Amount = -50.00m,
                TransactionDate = baseDate, // Jan 15, 2024
                Description = "Local Market",
                AccountId = 1,
                CategoryId = 1,
                Status = TransactionStatus.Cleared,
                IsReviewed = true
            },
            new Transaction
            {
                Id = 4,
                Amount = -80.75m,
                TransactionDate = baseDate.AddDays(5), // Jan 20, 2024
                Description = "Whole Foods",
                AccountId = 1,
                CategoryId = 1,
                Status = TransactionStatus.Cleared,
                IsReviewed = true
            },

            // Salary category transactions
            new Transaction
            {
                Id = 5,
                Amount = 2000.00m,
                TransactionDate = baseDate.AddDays(-30), // Dec 16, 2023
                Description = "Monthly Salary",
                AccountId = 2,
                CategoryId = 2,
                Status = TransactionStatus.Cleared,
                IsReviewed = true
            },
            new Transaction
            {
                Id = 6,
                Amount = 2000.00m,
                TransactionDate = baseDate, // Jan 15, 2024
                Description = "Monthly Salary",
                AccountId = 2,
                CategoryId = 2,
                Status = TransactionStatus.Cleared,
                IsReviewed = true
            },

            // Unreviewed transaction
            new Transaction
            {
                Id = 7,
                Amount = -25.00m,
                TransactionDate = baseDate.AddDays(-2), // Jan 13, 2024
                Description = "Coffee Shop",
                AccountId = 1,
                CategoryId = 1,
                Status = TransactionStatus.Cleared,
                IsReviewed = false
            },

            // Different account
            new Transaction
            {
                Id = 8,
                Amount = -200.00m,
                TransactionDate = baseDate.AddDays(-1), // Jan 14, 2024
                Description = "Savings Transfer",
                AccountId = 2,
                CategoryId = 1,
                Status = TransactionStatus.Cleared,
                IsReviewed = true
            },

            // Other user's transaction (should not appear in results)
            new Transaction
            {
                Id = 9,
                Amount = -150.00m,
                TransactionDate = baseDate,
                Description = "Other User Transaction",
                AccountId = 3,
                CategoryId = 1,
                Status = TransactionStatus.Cleared,
                IsReviewed = true
            },

            // Future transaction
            new Transaction
            {
                Id = 10,
                Amount = -300.00m,
                TransactionDate = baseDate.AddDays(30), // Feb 14, 2024
                Description = "Future Expense",
                AccountId = 1,
                CategoryId = 1,
                Status = TransactionStatus.Cleared,
                IsReviewed = true
            }
        };

        _context.Transactions.AddRange(transactions);
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetFilteredAsync_WithCategoryId_ReturnsOnlyTransactionsInCategory()
    {
        // Arrange
        var query = new GetTransactionsQuery
        {
            UserId = _userId,
            CategoryId = 1, // Groceries category
            Page = 1,
            PageSize = 100
        };

        // Act
        var (transactions, totalCount) = await _repository.GetFilteredAsync(query);
        var transactionList = transactions.ToList();

        // Assert
        Assert.Equal(7, totalCount); // 7 transactions in groceries category for this user
        Assert.All(transactionList, t => Assert.Equal(1, t.CategoryId));
        Assert.All(transactionList, t => Assert.Equal(_userId, t.Account.UserId));
    }

    [Fact]
    public async Task GetFilteredAsync_WithDateRange_ReturnsTransactionsInRange()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2024, 1, 20, 23, 59, 59, DateTimeKind.Utc);

        var query = new GetTransactionsQuery
        {
            UserId = _userId,
            CategoryId = 1, // Groceries category
            StartDate = startDate,
            EndDate = endDate,
            Page = 1,
            PageSize = 100
        };

        // Act
        var (transactions, totalCount) = await _repository.GetFilteredAsync(query);
        var transactionList = transactions.ToList();

        // Assert
        // Should include transactions from Jan 10, 13, 14, 15, 20 (5 transactions)
        Assert.Equal(5, totalCount);
        Assert.All(transactionList, t => Assert.True(t.TransactionDate >= startDate));
        Assert.All(transactionList, t => Assert.True(t.TransactionDate <= endDate));
    }

    [Fact]
    public async Task GetFilteredAsync_WithCategoryAndDateRange_CalculatesCorrectStats()
    {
        // Arrange - Test "This Month" scenario (Jan 1-15)
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2024, 1, 15, 23, 59, 59, DateTimeKind.Utc);

        var query = new GetTransactionsQuery
        {
            UserId = _userId,
            CategoryId = 1, // Groceries category
            StartDate = startDate,
            EndDate = endDate,
            Page = 1,
            PageSize = 1000 // Get all transactions for stats calculation
        };

        // Act
        var (transactions, totalCount) = await _repository.GetFilteredAsync(query);
        var transactionList = transactions.ToList();

        // Assert
        // Expected: Jan 5 (-100.50), Jan 10 (-75.25), Jan 13 (-25.00), Jan 14 (-200.00), Jan 15 (-50.00)
        Assert.Equal(5, totalCount);

        // Calculate stats like frontend does
        var totalAmount = transactionList.Sum(t => t.Amount);
        var averageAmount = transactionList.Count > 0 ? totalAmount / transactionList.Count : 0;

        Assert.Equal(-450.75m, totalAmount); // Sum of all amounts
        Assert.Equal(-90.15m, averageAmount); // Average amount
        Assert.Equal(5, transactionList.Count);
    }

    [Fact]
    public async Task GetFilteredAsync_WithLast7Days_ReturnsCorrectTransactions()
    {
        // Arrange - Test "Last 7 Days" from Jan 15 (Jan 9-15)
        var endDate = new DateTime(2024, 1, 15, 23, 59, 59, DateTimeKind.Utc);
        var startDate = new DateTime(2024, 1, 9, 0, 0, 0, DateTimeKind.Utc);

        var query = new GetTransactionsQuery
        {
            UserId = _userId,
            CategoryId = 1, // Groceries category
            StartDate = startDate,
            EndDate = endDate,
            Page = 1,
            PageSize = 100
        };

        // Act
        var (transactions, totalCount) = await _repository.GetFilteredAsync(query);
        var transactionList = transactions.ToList();

        // Assert
        // Expected: Jan 10 (-75.25), Jan 13 (-25.00), Jan 14 (-200.00), Jan 15 (-50.00)
        Assert.Equal(4, totalCount);

        var totalAmount = transactionList.Sum(t => t.Amount);
        Assert.Equal(-350.25m, totalAmount);
    }

    [Fact]
    public async Task GetFilteredAsync_WithLast30Days_ReturnsCorrectTransactions()
    {
        // Arrange - Test "Last 30 Days" from Jan 15 (Dec 17 - Jan 15)
        var endDate = new DateTime(2024, 1, 15, 23, 59, 59, DateTimeKind.Utc);
        var startDate = new DateTime(2023, 12, 17, 0, 0, 0, DateTimeKind.Utc);

        var query = new GetTransactionsQuery
        {
            UserId = _userId,
            CategoryId = 1, // Groceries category
            StartDate = startDate,
            EndDate = endDate,
            Page = 1,
            PageSize = 100
        };

        // Act
        var (transactions, totalCount) = await _repository.GetFilteredAsync(query);
        var transactionList = transactions.ToList();

        // Assert
        // Expected: All groceries transactions within the 30-day range
        Assert.Equal(5, totalCount); // Same as "this month" in our test data
        
        var totalAmount = transactionList.Sum(t => t.Amount);
        Assert.Equal(-450.75m, totalAmount);
    }

    [Fact]
    public async Task GetFilteredAsync_WithAllTime_ReturnsAllTransactions()
    {
        // Arrange - Test "All Time" (no date filters)
        var query = new GetTransactionsQuery
        {
            UserId = _userId,
            CategoryId = 1, // Groceries category
            Page = 1,
            PageSize = 100
        };

        // Act
        var (transactions, totalCount) = await _repository.GetFilteredAsync(query);
        var transactionList = transactions.ToList();

        // Assert
        // Expected: All 7 groceries transactions for this user
        Assert.Equal(7, totalCount);
        
        var totalAmount = transactionList.Sum(t => t.Amount);
        Assert.Equal(-831.50m, totalAmount); // All 7 transactions: -100.50-75.25-50.00-80.75-25.00-200.00-300.00
    }

    [Fact]
    public async Task GetFilteredAsync_WithReviewedFilter_ReturnsOnlyReviewedTransactions()
    {
        // Arrange
        var query = new GetTransactionsQuery
        {
            UserId = _userId,
            CategoryId = 1, // Groceries category
            IsReviewed = true,
            Page = 1,
            PageSize = 100
        };

        // Act
        var (transactions, totalCount) = await _repository.GetFilteredAsync(query);
        var transactionList = transactions.ToList();

        // Assert
        // Should exclude the unreviewed coffee shop transaction
        Assert.Equal(6, totalCount); // 7 total - 1 unreviewed
        Assert.All(transactionList, t => Assert.True(t.IsReviewed));
    }

    [Fact]
    public async Task GetFilteredAsync_WithAccountFilter_ReturnsOnlyAccountTransactions()
    {
        // Arrange
        var query = new GetTransactionsQuery
        {
            UserId = _userId,
            CategoryId = 1, // Groceries category
            AccountId = 1, // Checking account only
            Page = 1,
            PageSize = 100
        };

        // Act
        var (transactions, totalCount) = await _repository.GetFilteredAsync(query);
        var transactionList = transactions.ToList();

        // Assert
        // Should exclude the savings account transaction (-200.00)
        Assert.Equal(6, totalCount); // 7 total - 1 from savings account
        Assert.All(transactionList, t => Assert.Equal(1, t.AccountId));
    }

    [Fact]
    public async Task GetFilteredAsync_UserIsolation_DoesNotReturnOtherUsersTransactions()
    {
        // Arrange
        var query = new GetTransactionsQuery
        {
            UserId = _userId,
            Page = 1,
            PageSize = 100
        };

        // Act
        var (transactions, totalCount) = await _repository.GetFilteredAsync(query);
        var transactionList = transactions.ToList();

        // Assert
        // Should not include other user's transaction
        Assert.All(transactionList, t => Assert.Equal(_userId, t.Account.UserId));
        Assert.DoesNotContain(transactionList, t => t.Id == 9); // Other user's transaction
    }

    [Fact]
    public async Task GetFilteredAsync_EdgeCaseDateBoundaries_HandlesInclusiveDates()
    {
        // Arrange - Test exact date boundaries
        var exactDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        var query = new GetTransactionsQuery
        {
            UserId = _userId,
            StartDate = exactDate,
            EndDate = exactDate,
            Page = 1,
            PageSize = 100
        };

        // Act
        var (transactions, totalCount) = await _repository.GetFilteredAsync(query);
        var transactionList = transactions.ToList();

        // Assert
        // Should include transactions from exactly Jan 15
        var jan15Transactions = transactionList.Where(t => 
            t.TransactionDate.Date == exactDate.Date).ToList();
        
        Assert.True(jan15Transactions.Count > 0);
        Assert.All(jan15Transactions, t => Assert.Equal(exactDate.Date, t.TransactionDate.Date));
    }

    [Theory]
    [InlineData("amount", "asc")]
    [InlineData("amount", "desc")]
    [InlineData("transactiondate", "asc")]
    [InlineData("transactiondate", "desc")]
    public async Task GetFilteredAsync_WithSorting_ReturnsCorrectlyOrderedResults(string sortBy, string sortDirection)
    {
        // Arrange
        var query = new GetTransactionsQuery
        {
            UserId = _userId,
            CategoryId = 1, // Groceries category
            SortBy = sortBy,
            SortDirection = sortDirection,
            Page = 1,
            PageSize = 100
        };

        // Act
        var (transactions, totalCount) = await _repository.GetFilteredAsync(query);
        var transactionList = transactions.ToList();

        // Assert
        Assert.True(totalCount > 1); // Need multiple transactions to test sorting

        if (sortBy.ToLower() == "amount")
        {
            // Note: Repository sorts by absolute value for better UX (smaller expenses first in ascending order)
            var amounts = transactionList.Select(t => t.Amount).ToList();
            if (sortDirection == "asc")
            {
                Assert.Equal(amounts.OrderBy(a => Math.Abs(a)), amounts);
            }
            else
            {
                Assert.Equal(amounts.OrderByDescending(a => Math.Abs(a)), amounts);
            }
        }
        else if (sortBy.ToLower() == "transactiondate")
        {
            var dates = transactionList.Select(t => t.TransactionDate).ToList();
            if (sortDirection == "asc")
            {
                Assert.Equal(dates.OrderBy(d => d), dates);
            }
            else
            {
                Assert.Equal(dates.OrderByDescending(d => d), dates);
            }
        }
    }

    [Fact]
    public async Task GetFilteredAsync_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var query = new GetTransactionsQuery
        {
            UserId = _userId,
            Page = 2,
            PageSize = 2,
            SortBy = "TransactionDate",
            SortDirection = "desc"
        };

        // Act
        var (transactions, totalCount) = await _repository.GetFilteredAsync(query);
        var transactionList = transactions.ToList();

        // Assert
        Assert.True(totalCount > 4); // Need enough transactions for page 2
        Assert.Equal(2, transactionList.Count); // Should return exactly 2 transactions for page 2
    }

    [Fact]
    public async Task GetSummaryAsync_WithDateFilter_CalculatesCorrectBalance()
    {
        // Arrange
        var endDate = new DateTime(2024, 1, 15, 23, 59, 59, DateTimeKind.Utc);
        var query = new GetTransactionsQuery
        {
            UserId = _userId,
            AccountId = 1, // Checking Account
            EndDate = endDate
        };

        // Act
        var summary = await _repository.GetSummaryAsync(query);

        // Assert
        // Initial balance of checking account is 1000
        // Transactions for account 1 up to Jan 15:
        // -100.50 (Jan 5)
        // -75.25 (Jan 10)
        // -50.00 (Jan 15)
        // -25.00 (Jan 13)
        // Total transactions = -250.75
        // Expected balance = 1000 - 250.75 = 749.25
        Assert.Equal(749.25m, summary.TotalBalance);
    }

    [Fact]
    public async Task GetMonthlySpendingAsync_WithCancelledTransaction_ExcludesItFromCalculation()
    {
        // Arrange
        var accountId = 1; // Checking Account
        var now = DateTime.UtcNow;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Add a cancelled transaction in the current month
        var cancelledTransaction = new Transaction
        {
            Id = 11,
            Amount = -50.00m,
            TransactionDate = currentMonthStart.AddDays(5),
            Description = "Cancelled Expense",
            AccountId = accountId,
            CategoryId = 1,
            Status = TransactionStatus.Cancelled,
            IsReviewed = true
        };
        _context.Transactions.Add(cancelledTransaction);

        // Add a valid transaction in the current month
        var validTransaction = new Transaction
        {
            Id = 12,
            Amount = -100.00m,
            TransactionDate = currentMonthStart.AddDays(10),
            Description = "Valid Expense",
            AccountId = accountId,
            CategoryId = 1,
            Status = TransactionStatus.Cleared,
            IsReviewed = true
        };
        _context.Transactions.Add(validTransaction);

        await _context.SaveChangesAsync();

        // Act
        var (currentMonth, previousMonth) = await _repository.GetMonthlySpendingAsync(accountId, _userId);

        // Assert
        // Spending for the current month should only include the valid transaction.
        Assert.Equal(100.00m, currentMonth);
    }

    [Fact]
    public async Task GetCategorizedTransactionIdsAsync_WithEmptyList_ReturnsEmpty()
    {
        // Arrange
        var transactionIds = new List<int>();

        // Act
        var result = await _repository.GetCategorizedTransactionIdsAsync(transactionIds);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCategorizedTransactionIdsAsync_WithCategorizedTransactions_ReturnsOnlyCategorized()
    {
        // Arrange
        var transactionIds = new[] { 1, 2, 3, 4, 5 };

        // Act
        var result = await _repository.GetCategorizedTransactionIdsAsync(transactionIds);

        // Assert
        // From our test data, transactions 1-7 should have categories (CategoryId = 1)
        // Transaction 8 is uncategorized (CategoryId = null)
        var expectedCategorized = transactionIds.Where(id => id <= 7).ToHashSet();
        Assert.Equal(expectedCategorized, result);
        Assert.Contains(1, result);
        Assert.Contains(2, result);
        Assert.DoesNotContain(8, result); // This one is uncategorized
    }

    [Fact]
    public async Task GetCategorizedTransactionIdsAsync_WithNonexistentIds_IgnoresMissing()
    {
        // Arrange
        var transactionIds = new[] { 1, 2, 999, 1000 }; // 999 and 1000 don't exist

        // Act
        var result = await _repository.GetCategorizedTransactionIdsAsync(transactionIds);

        // Assert
        // Should only return existing categorized transactions
        var expectedCategorized = new HashSet<int> { 1, 2 };
        Assert.Equal(expectedCategorized, result);
        Assert.DoesNotContain(999, result);
        Assert.DoesNotContain(1000, result);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
