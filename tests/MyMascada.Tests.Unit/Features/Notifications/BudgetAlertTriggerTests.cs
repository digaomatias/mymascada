using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Budgets.DTOs;
using MyMascada.Application.Features.Budgets.Services;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Services.Notifications;
using NSubstitute.ExceptionExtensions;

namespace MyMascada.Tests.Unit.Features.Notifications;

public class BudgetAlertTriggerTests
{
    private readonly INotificationService _notificationService;
    private readonly IBudgetRepository _budgetRepo;
    private readonly IBudgetCalculationService _budgetCalc;
    private readonly INotificationPreferenceRepository _preferenceRepo;
    private readonly NotificationTriggerService _sut;
    private readonly Guid _userId = Guid.NewGuid();
    private static readonly DateTime PeriodStart = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    public BudgetAlertTriggerTests()
    {
        _notificationService = Substitute.For<INotificationService>();
        _budgetRepo = Substitute.For<IBudgetRepository>();
        _budgetCalc = Substitute.For<IBudgetCalculationService>();
        _preferenceRepo = Substitute.For<INotificationPreferenceRepository>();
        _sut = new NotificationTriggerService(
            _notificationService,
            Substitute.For<ITransactionRepository>(),
            _budgetRepo,
            _budgetCalc,
            _preferenceRepo,
            Substitute.For<ILogger<NotificationTriggerService>>());
    }

    // ---- helpers ---------------------------------------------------------

    private static BudgetCategoryProgressDto Cat(
        decimal effectiveBudget, decimal actualSpent, decimal usedPct,
        bool over, bool approaching, int categoryId = 7, string name = "Groceries")
        => new()
        {
            CategoryId = categoryId,
            CategoryName = name,
            EffectiveBudget = effectiveBudget,
            ActualSpent = actualSpent,
            UsedPercentage = usedPct,
            IsOverBudget = over,
            IsApproachingLimit = approaching
        };

    private void SetupBudget(int budgetId, int? userThreshold, params BudgetCategoryProgressDto[] categories)
    {
        var budget = new Budget { Id = budgetId, UserId = _userId, StartDate = PeriodStart, Status = BudgetStatus.Active };
        _budgetRepo.GetActiveBudgetsForUserAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new List<Budget> { budget });

        _preferenceRepo.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(userThreshold is null ? null : new NotificationPreference { BudgetAlertPercentage = userThreshold });

        var detail = new BudgetDetailDto
        {
            Id = budgetId,
            StartDate = PeriodStart,
            Categories = categories.ToList()
        };
        _budgetCalc.CalculateBudgetProgressAsync(Arg.Any<Budget>(), _userId, Arg.Any<CancellationToken>())
            .Returns(detail);
    }

    // ---- tests -----------------------------------------------------------

    [Fact]
    public async Task CheckBudgetThresholds_CategoryApproaching_CreatesBudgetThresholdNotification()
    {
        SetupBudget(42, userThreshold: null, Cat(effectiveBudget: 500, actualSpent: 425, usedPct: 85, over: false, approaching: true));

        await _sut.CheckBudgetThresholdsAsync(_userId);

        await _notificationService.Received(1).CreateNotificationAsync(
            _userId, NotificationType.BudgetThreshold, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<NotificationPriority>(), Arg.Any<string?>(),
            Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckBudgetThresholds_CategoryExceeded_CreatesBudgetExceededNotification()
    {
        SetupBudget(42, userThreshold: null, Cat(effectiveBudget: 500, actualSpent: 560, usedPct: 112, over: true, approaching: false));

        await _sut.CheckBudgetThresholdsAsync(_userId);

        await _notificationService.Received(1).CreateNotificationAsync(
            _userId, NotificationType.BudgetExceeded, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), NotificationPriority.High, Arg.Any<string?>(),
            Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckBudgetThresholds_Exceeded_FiresOnlyExceededNotApproaching()
    {
        // A category over budget is also (numerically) past the approaching
        // threshold — only the more severe BudgetExceeded must fire.
        SetupBudget(42, userThreshold: null, Cat(effectiveBudget: 500, actualSpent: 560, usedPct: 112, over: true, approaching: false));

        await _sut.CheckBudgetThresholdsAsync(_userId);

        await _notificationService.DidNotReceive().CreateNotificationAsync(
            _userId, NotificationType.BudgetThreshold, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<NotificationPriority>(), Arg.Any<string?>(),
            Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckBudgetThresholds_BelowThreshold_CreatesNoNotification()
    {
        SetupBudget(42, userThreshold: null, Cat(effectiveBudget: 500, actualSpent: 350, usedPct: 70, over: false, approaching: false));

        await _sut.CheckBudgetThresholdsAsync(_userId);

        await _notificationService.DidNotReceive().CreateNotificationAsync(
            Arg.Any<Guid>(), Arg.Any<NotificationType>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<NotificationPriority>(), Arg.Any<string?>(),
            Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckBudgetThresholds_ZeroEffectiveBudget_SkipsCategory()
    {
        // An unbudgeted category (effective budget 0) reports over-budget for any
        // spend — it must be skipped so users aren't spammed about categories they
        // never budgeted for.
        SetupBudget(42, userThreshold: null, Cat(effectiveBudget: 0, actualSpent: 30, usedPct: 100, over: true, approaching: false));

        await _sut.CheckBudgetThresholdsAsync(_userId);

        await _notificationService.DidNotReceive().CreateNotificationAsync(
            Arg.Any<Guid>(), Arg.Any<NotificationType>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<NotificationPriority>(), Arg.Any<string?>(),
            Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckBudgetThresholds_UserThresholdHigherThanSpend_DoesNotFire()
    {
        // User raised their alert threshold to 90%; a category at 85% must not alert.
        SetupBudget(42, userThreshold: 90, Cat(effectiveBudget: 500, actualSpent: 425, usedPct: 85, over: false, approaching: true));

        await _sut.CheckBudgetThresholdsAsync(_userId);

        await _notificationService.DidNotReceive().CreateNotificationAsync(
            Arg.Any<Guid>(), Arg.Any<NotificationType>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<NotificationPriority>(), Arg.Any<string?>(),
            Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckBudgetThresholds_UserThresholdMet_Fires()
    {
        // Same 90% preference, but now the category is at 92% — it should fire.
        SetupBudget(42, userThreshold: 90, Cat(effectiveBudget: 500, actualSpent: 460, usedPct: 92, over: false, approaching: true));

        await _sut.CheckBudgetThresholdsAsync(_userId);

        await _notificationService.Received(1).CreateNotificationAsync(
            _userId, NotificationType.BudgetThreshold, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<NotificationPriority>(), Arg.Any<string?>(),
            Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckBudgetThresholds_GroupKeyIsPeriodScopedNotRunDate()
    {
        // The idempotency key must encode the budget period start, NOT the date the
        // job ran — otherwise a user gets a fresh alert every single day.
        SetupBudget(42, userThreshold: null, Cat(effectiveBudget: 500, actualSpent: 425, usedPct: 85, over: false, approaching: true, categoryId: 7));

        await _sut.CheckBudgetThresholdsAsync(_userId);

        await _notificationService.Received(1).CreateNotificationAsync(
            _userId, NotificationType.BudgetThreshold, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<NotificationPriority>(),
            Arg.Is<string?>(key => key == "budget:42:cat:7:threshold:approaching:2026-06-01"),
            Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckBudgetThresholds_NoActiveBudgets_CreatesNoNotification()
    {
        _budgetRepo.GetActiveBudgetsForUserAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new List<Budget>());

        await _sut.CheckBudgetThresholdsAsync(_userId);

        await _notificationService.DidNotReceive().CreateNotificationAsync(
            Arg.Any<Guid>(), Arg.Any<NotificationType>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<NotificationPriority>(), Arg.Any<string?>(),
            Arg.Any<DateTime?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckBudgetThresholds_WhenDependencyThrows_PropagatesToCaller()
    {
        // The trigger must NOT swallow exceptions — BudgetAlertJobService relies
        // on them propagating so it can isolate the failure per user, aggregate,
        // and let Hangfire retry. Swallowing would silently skip the user's alerts.
        _budgetRepo.GetActiveBudgetsForUserAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new List<Budget>
            {
                new() { Id = 42, UserId = _userId, StartDate = PeriodStart, Status = BudgetStatus.Active }
            });
        _preferenceRepo.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>()).Returns((NotificationPreference?)null);
        _budgetCalc.CalculateBudgetProgressAsync(Arg.Any<Budget>(), _userId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("transient db error"));

        var act = () => _sut.CheckBudgetThresholdsAsync(_userId);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
