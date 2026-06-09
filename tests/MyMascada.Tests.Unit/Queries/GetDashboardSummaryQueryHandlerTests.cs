using FluentAssertions;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Reports.Queries;
using MyMascada.Domain.Common;
using MyMascada.Domain.Entities;
using NSubstitute;
using Xunit;

namespace MyMascada.Tests.Unit.Queries;

/// <summary>
/// Regression tests for the dashboard summary. The key invariant being protected
/// here is that the displayed-period figures (MonthlyIncome, MonthlyExpenses,
/// NetSaved, SavingsRate) must all describe the SAME period. When the current
/// month is empty and the handler falls back to a previous month, NetSaved and
/// SavingsRate must be derived from the displayed (fallback) month's
/// income/expenses — NOT from the empty current month, and NOT from the
/// 3-month rolling averages.
/// </summary>
public class GetDashboardSummaryQueryHandlerTests
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly GetDashboardSummaryQueryHandler _handler;

    public GetDashboardSummaryQueryHandlerTests()
    {
        _transactionRepository = Substitute.For<ITransactionRepository>();
        _accountRepository = Substitute.For<IAccountRepository>();
        _handler = new GetDashboardSummaryQueryHandler(_accountRepository, _transactionRepository);
    }

    // Returns the first day (UTC midnight) of the month that contains the given date.
    private static DateTime MonthStart(DateTime date) =>
        new(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_WhenCurrentMonthEmpty_FallsBackAndKeepsDisplayedFiguresConsistent()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _accountRepository.GetByUserIdAsync(userId).Returns(new List<Account>());
        _transactionRepository.GetAccountBalancesAsync(userId)
            .Returns(new Dictionary<int, decimal>());
        _transactionRepository.GetRecentAsync(userId, 5).Returns(new List<Transaction>());
        _transactionRepository.GetCountByUserIdAsync(userId).Returns(2);

        // Route repository calls relative to the start date the handler passes in,
        // derived from that argument (not from a separately captured "now"), so the
        // test is deterministic even if the clock crosses a month boundary mid-test:
        //  - the current month (start == this month's 1st) => empty (triggers fallback)
        //  - the immediately preceding month               => populated fallback data
        //  - any other range (3-month averages)            => empty
        // The fallback month has income 2000 and expenses 573 => netSaved 1427.
        _transactionRepository
            .GetByDateRangeAsync(userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(callInfo =>
            {
                var start = callInfo.ArgAt<DateTime>(1);
                var currentMonthStart = MonthStart(DateTimeProvider.UtcNow);
                var fallbackMonthStart = currentMonthStart.AddMonths(-1);

                if (start == fallbackMonthStart)
                {
                    var fallbackTransactions = new List<Transaction>
                    {
                        new() { Id = 1, AccountId = 1, Amount = 2000m, TransactionDate = fallbackMonthStart.AddDays(2), Description = "Salary" },
                        new() { Id = 2, AccountId = 1, Amount = -573m, TransactionDate = fallbackMonthStart.AddDays(5), Description = "Rent" },
                    };
                    return Task.FromResult<IEnumerable<Transaction>>(fallbackTransactions);
                }

                return Task.FromResult<IEnumerable<Transaction>>(new List<Transaction>());
            });

        var query = new GetDashboardSummaryQuery { UserId = userId };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert — fallback engaged and pointing at the previous month
        var expectedFallback = MonthStart(DateTimeProvider.UtcNow).AddMonths(-1);
        result.IsUsingFallbackMonth.Should().BeTrue("the current month has no non-transfer transactions");
        result.DisplayMonth.Should().Be(expectedFallback.Month);
        result.DisplayYear.Should().Be(expectedFallback.Year);

        // Assert — displayed figures come from the fallback month, not the empty current month
        result.MonthlyIncome.Should().Be(2000m);
        result.MonthlyExpenses.Should().Be(573m);

        // CRITICAL invariant: NetSaved == displayed income - displayed expenses
        result.NetSaved.Should().Be(result.MonthlyIncome - result.MonthlyExpenses);
        result.NetSaved.Should().Be(1427m);

        // CRITICAL invariant: SavingsRate is derived from the same displayed figures
        var expectedSavingsRate = Math.Round((result.NetSaved / result.MonthlyIncome) * 100, 0);
        result.SavingsRate.Should().Be(expectedSavingsRate);
    }

    [Fact]
    public async Task Handle_WhenCurrentMonthHasTransactions_UsesCurrentMonthConsistently()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _accountRepository.GetByUserIdAsync(userId).Returns(new List<Account>());
        _transactionRepository.GetAccountBalancesAsync(userId)
            .Returns(new Dictionary<int, decimal>());
        _transactionRepository.GetRecentAsync(userId, 5).Returns(new List<Transaction>());
        _transactionRepository.GetCountByUserIdAsync(userId).Returns(2);

        // Populate the current month (income 1000, expenses 250). Routing is derived
        // from the start argument relative to the current month, so it stays
        // deterministic across a month boundary.
        _transactionRepository
            .GetByDateRangeAsync(userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(callInfo =>
            {
                var start = callInfo.ArgAt<DateTime>(1);
                var currentMonthStart = MonthStart(DateTimeProvider.UtcNow);

                if (start == currentMonthStart)
                {
                    var currentTransactions = new List<Transaction>
                    {
                        new() { Id = 1, AccountId = 1, Amount = 1000m, TransactionDate = currentMonthStart.AddDays(1), Description = "Salary" },
                        new() { Id = 2, AccountId = 1, Amount = -250m, TransactionDate = currentMonthStart.AddDays(1), Description = "Groceries" },
                    };
                    return Task.FromResult<IEnumerable<Transaction>>(currentTransactions);
                }

                return Task.FromResult<IEnumerable<Transaction>>(new List<Transaction>());
            });

        var query = new GetDashboardSummaryQuery { UserId = userId };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        var expectedCurrent = MonthStart(DateTimeProvider.UtcNow);
        result.IsUsingFallbackMonth.Should().BeFalse("the current month has non-transfer transactions");
        result.DisplayMonth.Should().Be(expectedCurrent.Month);
        result.DisplayYear.Should().Be(expectedCurrent.Year);

        result.MonthlyIncome.Should().Be(1000m);
        result.MonthlyExpenses.Should().Be(250m);

        // Invariant holds for the non-fallback path too
        result.NetSaved.Should().Be(result.MonthlyIncome - result.MonthlyExpenses);
        result.NetSaved.Should().Be(750m);

        var expectedSavingsRate = Math.Round((result.NetSaved / result.MonthlyIncome) * 100, 0);
        result.SavingsRate.Should().Be(expectedSavingsRate);
    }
}
