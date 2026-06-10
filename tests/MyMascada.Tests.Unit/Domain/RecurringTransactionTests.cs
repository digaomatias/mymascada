using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Tests.Unit.Domain;

public class RecurringTransactionTests
{
    private static RecurringTransaction CreateSchedule(
        RecurrenceFrequency frequency,
        DateTime startDate,
        int? customIntervalDays = null,
        DateTime? endDate = null)
    {
        return new RecurringTransaction
        {
            Id = 1,
            UserId = Guid.NewGuid(),
            AccountId = 1,
            Description = "Test bill",
            Amount = 50m,
            Frequency = frequency,
            CustomIntervalDays = customIntervalDays,
            StartDate = startDate,
            EndDate = endDate,
            NextDueDate = startDate,
            IsActive = true
        };
    }

    // --- CalculateNextDueDate: simple day-based frequencies ---

    [Fact]
    public void CalculateNextDueDate_Weekly_Adds7Days()
    {
        var schedule = CreateSchedule(RecurrenceFrequency.Weekly, new DateTime(2026, 6, 1));

        var next = schedule.CalculateNextDueDate(new DateTime(2026, 6, 1));

        next.Should().Be(new DateTime(2026, 6, 8));
    }

    [Fact]
    public void CalculateNextDueDate_Fortnightly_Adds14Days()
    {
        var schedule = CreateSchedule(RecurrenceFrequency.Fortnightly, new DateTime(2026, 6, 1));

        var next = schedule.CalculateNextDueDate(new DateTime(2026, 6, 1));

        next.Should().Be(new DateTime(2026, 6, 15));
    }

    [Fact]
    public void CalculateNextDueDate_Custom_AddsConfiguredInterval()
    {
        var schedule = CreateSchedule(RecurrenceFrequency.Custom, new DateTime(2026, 6, 1), customIntervalDays: 45);

        var next = schedule.CalculateNextDueDate(new DateTime(2026, 6, 1));

        next.Should().Be(new DateTime(2026, 7, 16));
    }

    [Fact]
    public void CalculateNextDueDate_CustomWithoutInterval_Throws()
    {
        var schedule = CreateSchedule(RecurrenceFrequency.Custom, new DateTime(2026, 6, 1), customIntervalDays: null);

        var act = () => schedule.CalculateNextDueDate(new DateTime(2026, 6, 1));

        act.Should().Throw<InvalidOperationException>();
    }

    // --- Monthly: calendar math with end-of-month clamping ---

    [Fact]
    public void CalculateNextDueDate_Monthly_UsesCalendarMonths()
    {
        var schedule = CreateSchedule(RecurrenceFrequency.Monthly, new DateTime(2026, 6, 15));

        var next = schedule.CalculateNextDueDate(new DateTime(2026, 6, 15));

        next.Should().Be(new DateTime(2026, 7, 15));
    }

    [Fact]
    public void CalculateNextDueDate_MonthlyDue31st_ClampsToFebruary28()
    {
        var schedule = CreateSchedule(RecurrenceFrequency.Monthly, new DateTime(2026, 1, 31));

        var next = schedule.CalculateNextDueDate(new DateTime(2026, 1, 31));

        next.Should().Be(new DateTime(2026, 2, 28)); // 2026 is not a leap year
    }

    [Fact]
    public void CalculateNextDueDate_MonthlyDue31st_LeapYearClampsToFebruary29()
    {
        var schedule = CreateSchedule(RecurrenceFrequency.Monthly, new DateTime(2028, 1, 31));

        var next = schedule.CalculateNextDueDate(new DateTime(2028, 1, 31));

        next.Should().Be(new DateTime(2028, 2, 29)); // 2028 is a leap year
    }

    [Fact]
    public void CalculateNextDueDate_MonthlyDue31st_RecoversAnchorDayAfterShortMonth()
    {
        // Anchored on the 31st: Jan 31 -> Feb 28 -> Mar 31 (not Mar 28)
        var schedule = CreateSchedule(RecurrenceFrequency.Monthly, new DateTime(2026, 1, 31));

        var february = schedule.CalculateNextDueDate(new DateTime(2026, 1, 31));
        var march = schedule.CalculateNextDueDate(february);

        february.Should().Be(new DateTime(2026, 2, 28));
        march.Should().Be(new DateTime(2026, 3, 31));
    }

    [Fact]
    public void CalculateNextDueDate_MonthlyDue30th_ClampsOnlyInFebruary()
    {
        var schedule = CreateSchedule(RecurrenceFrequency.Monthly, new DateTime(2026, 1, 30));

        var february = schedule.CalculateNextDueDate(new DateTime(2026, 1, 30));
        var march = schedule.CalculateNextDueDate(february);
        var april = schedule.CalculateNextDueDate(march);

        february.Should().Be(new DateTime(2026, 2, 28));
        march.Should().Be(new DateTime(2026, 3, 30));
        april.Should().Be(new DateTime(2026, 4, 30));
    }

    // --- Yearly: calendar math with leap-day clamping ---

    [Fact]
    public void CalculateNextDueDate_Yearly_UsesCalendarYears()
    {
        var schedule = CreateSchedule(RecurrenceFrequency.Yearly, new DateTime(2026, 3, 10));

        var next = schedule.CalculateNextDueDate(new DateTime(2026, 3, 10));

        next.Should().Be(new DateTime(2027, 3, 10));
    }

    [Fact]
    public void CalculateNextDueDate_YearlyOnLeapDay_ClampsToFeb28ThenRecovers()
    {
        var schedule = CreateSchedule(RecurrenceFrequency.Yearly, new DateTime(2024, 2, 29));

        var y2025 = schedule.CalculateNextDueDate(new DateTime(2024, 2, 29));
        var y2026 = schedule.CalculateNextDueDate(y2025);
        var y2027 = schedule.CalculateNextDueDate(y2026);
        var y2028 = schedule.CalculateNextDueDate(y2027);

        y2025.Should().Be(new DateTime(2025, 2, 28));
        y2026.Should().Be(new DateTime(2026, 2, 28));
        y2027.Should().Be(new DateTime(2027, 2, 28));
        y2028.Should().Be(new DateTime(2028, 2, 29)); // leap year recovers the anchor day
    }

    [Fact]
    public void CalculateNextDueDate_PreservesUtcKind()
    {
        var start = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);
        var schedule = CreateSchedule(RecurrenceFrequency.Monthly, start);

        var next = schedule.CalculateNextDueDate(start);

        next.Kind.Should().Be(DateTimeKind.Utc);
    }

    // --- AdvanceNextDueDate ---

    [Fact]
    public void AdvanceNextDueDate_SkipsAllMissedOccurrences()
    {
        var schedule = CreateSchedule(RecurrenceFrequency.Weekly, new DateTime(2026, 6, 1));

        // 3 weeks of missed runs: 6/1, 6/8, 6/15 are all <= 6/15
        schedule.AdvanceNextDueDate(new DateTime(2026, 6, 15));

        schedule.NextDueDate.Should().Be(new DateTime(2026, 6, 22));
        schedule.IsActive.Should().BeTrue();
    }

    [Fact]
    public void AdvanceNextDueDate_PastEndDate_DeactivatesSchedule()
    {
        var schedule = CreateSchedule(
            RecurrenceFrequency.Weekly,
            new DateTime(2026, 6, 1),
            endDate: new DateTime(2026, 6, 10));

        schedule.AdvanceNextDueDate(new DateTime(2026, 6, 8));

        schedule.NextDueDate.Should().Be(new DateTime(2026, 6, 15));
        schedule.IsActive.Should().BeFalse();
    }

    [Fact]
    public void AdvanceNextDueDate_BeforeEndDate_StaysActive()
    {
        var schedule = CreateSchedule(
            RecurrenceFrequency.Weekly,
            new DateTime(2026, 6, 1),
            endDate: new DateTime(2026, 12, 31));

        schedule.AdvanceNextDueDate(new DateTime(2026, 6, 1));

        schedule.NextDueDate.Should().Be(new DateTime(2026, 6, 8));
        schedule.IsActive.Should().BeTrue();
    }

    // --- AdvanceNextDueDate: far-behind schedules must snap fully past today ---

    [Fact]
    public void AdvanceNextDueDate_CustomDailyTwoYearsBehind_SnapsToTomorrow()
    {
        // A daily schedule 2 years behind has ~730 missed occurrences — far more
        // than any per-run iteration cap. It must still land strictly after today.
        var schedule = CreateSchedule(
            RecurrenceFrequency.Custom,
            new DateTime(2024, 6, 10),
            customIntervalDays: 1);
        var today = new DateTime(2026, 6, 10);

        schedule.AdvanceNextDueDate(today);

        schedule.NextDueDate.Should().Be(new DateTime(2026, 6, 11));
    }

    [Fact]
    public void AdvanceNextDueDate_WeeklyTenYearsBehind_SnapsPastTodayPreservingAnchor()
    {
        var start = new DateTime(2016, 6, 6); // a Monday
        var schedule = CreateSchedule(RecurrenceFrequency.Weekly, start);
        var today = new DateTime(2026, 6, 10);

        schedule.AdvanceNextDueDate(today);

        schedule.NextDueDate.Should().BeAfter(today);
        schedule.NextDueDate.Should().BeOnOrBefore(today.AddDays(7));
        schedule.NextDueDate.DayOfWeek.Should().Be(start.DayOfWeek); // anchor weekday preserved
        ((schedule.NextDueDate - start).Days % 7).Should().Be(0); // exact multiple of the interval
    }

    [Fact]
    public void AdvanceNextDueDate_MonthlyFortyYearsBehind_SnapsPastToday()
    {
        // 480 calendar steps — exceeds the old 366-iteration cap
        var schedule = CreateSchedule(RecurrenceFrequency.Monthly, new DateTime(1986, 1, 31));
        var today = new DateTime(2026, 6, 10);

        schedule.AdvanceNextDueDate(today);

        schedule.NextDueDate.Should().Be(new DateTime(2026, 6, 30)); // anchor day 31 clamped to June 30
    }

    [Fact]
    public void AdvanceNextDueDate_CustomDailyDueToday_AdvancesOneDay()
    {
        var schedule = CreateSchedule(
            RecurrenceFrequency.Custom,
            new DateTime(2026, 6, 10),
            customIntervalDays: 1);

        schedule.AdvanceNextDueDate(new DateTime(2026, 6, 10));

        schedule.NextDueDate.Should().Be(new DateTime(2026, 6, 11));
    }

    [Fact]
    public void Resume_AfterYearsLongPause_SnapsFullyPastToday()
    {
        var schedule = CreateSchedule(
            RecurrenceFrequency.Custom,
            new DateTime(2023, 1, 1),
            customIntervalDays: 1);
        schedule.Pause();

        schedule.Resume(new DateTime(2026, 6, 10));

        schedule.IsActive.Should().BeTrue();
        schedule.NextDueDate.Should().Be(new DateTime(2026, 6, 11));
    }

    // --- GetDueDates ---

    [Fact]
    public void GetDueDates_ReturnsAllDatesUpToToday()
    {
        var schedule = CreateSchedule(RecurrenceFrequency.Weekly, new DateTime(2026, 6, 1));

        var dueDates = schedule.GetDueDates(new DateTime(2026, 6, 15));

        dueDates.Should().Equal(
            new DateTime(2026, 6, 1),
            new DateTime(2026, 6, 8),
            new DateTime(2026, 6, 15));
    }

    [Fact]
    public void GetDueDates_NotYetDue_ReturnsEmpty()
    {
        var schedule = CreateSchedule(RecurrenceFrequency.Weekly, new DateTime(2026, 6, 10));

        var dueDates = schedule.GetDueDates(new DateTime(2026, 6, 9));

        dueDates.Should().BeEmpty();
    }

    [Fact]
    public void GetDueDates_StopsAtEndDate()
    {
        var schedule = CreateSchedule(
            RecurrenceFrequency.Weekly,
            new DateTime(2026, 6, 1),
            endDate: new DateTime(2026, 6, 10));

        var dueDates = schedule.GetDueDates(new DateTime(2026, 6, 30));

        dueDates.Should().Equal(
            new DateTime(2026, 6, 1),
            new DateTime(2026, 6, 8));
    }

    // --- GetScheduledDates (upcoming materialization) ---

    [Fact]
    public void GetScheduledDates_ReturnsDatesWithinWindow()
    {
        var schedule = CreateSchedule(RecurrenceFrequency.Weekly, new DateTime(2026, 6, 1));

        var dates = schedule.GetScheduledDates(new DateTime(2026, 6, 1), new DateTime(2026, 6, 20));

        dates.Should().Equal(
            new DateTime(2026, 6, 1),
            new DateTime(2026, 6, 8),
            new DateTime(2026, 6, 15));
    }

    [Fact]
    public void GetScheduledDates_RespectsEndDate()
    {
        var schedule = CreateSchedule(
            RecurrenceFrequency.Weekly,
            new DateTime(2026, 6, 1),
            endDate: new DateTime(2026, 6, 8));

        var dates = schedule.GetScheduledDates(new DateTime(2026, 6, 1), new DateTime(2026, 6, 30));

        dates.Should().Equal(
            new DateTime(2026, 6, 1),
            new DateTime(2026, 6, 8));
    }

    [Fact]
    public void GetScheduledDates_StaleNextDueDateYearsBehind_StillReturnsWindowDates()
    {
        // Daily schedule whose NextDueDate is 2 years stale (long pause / job
        // downtime). Walking day-by-day would burn all MaxCatchUpIterations
        // before reaching the window and return an empty list.
        var schedule = CreateSchedule(
            RecurrenceFrequency.Custom,
            new DateTime(2024, 6, 1),
            customIntervalDays: 1);

        var dates = schedule.GetScheduledDates(new DateTime(2026, 6, 10), new DateTime(2026, 6, 13));

        dates.Should().Equal(
            new DateTime(2026, 6, 10),
            new DateTime(2026, 6, 11),
            new DateTime(2026, 6, 12),
            new DateTime(2026, 6, 13));
    }

    [Fact]
    public void GetScheduledDates_StaleNextDueDate_PreservesScheduleAnchor()
    {
        // Weekly anchored on Monday 2025-06-02; a year later the materialized
        // dates must still fall on the same weekly grid (Mondays).
        var schedule = CreateSchedule(RecurrenceFrequency.Weekly, new DateTime(2025, 6, 2));

        var dates = schedule.GetScheduledDates(new DateTime(2026, 6, 10), new DateTime(2026, 6, 30));

        dates.Should().Equal(
            new DateTime(2026, 6, 15),
            new DateTime(2026, 6, 22),
            new DateTime(2026, 6, 29));
    }

    [Fact]
    public void GetScheduledDates_StaleMonthlySchedule_KeepsAnchorDayClamping()
    {
        // Monthly anchored on the 31st, stale for over a year: the window dates
        // must still clamp to month length (Jun 30) instead of drifting.
        var schedule = CreateSchedule(RecurrenceFrequency.Monthly, new DateTime(2025, 1, 31));

        var dates = schedule.GetScheduledDates(new DateTime(2026, 6, 1), new DateTime(2026, 7, 31));

        dates.Should().Equal(
            new DateTime(2026, 6, 30),
            new DateTime(2026, 7, 31));
    }

    // --- AdvanceNextDueDateAfter (incremental catch-up) ---

    [Fact]
    public void AdvanceNextDueDateAfter_SetsOccurrenceFollowingLastFiredDate()
    {
        var schedule = CreateSchedule(RecurrenceFrequency.Weekly, new DateTime(2026, 6, 1));

        // Run only processed up to 6/8 (cap reached); next due stays in the past
        schedule.AdvanceNextDueDateAfter(new DateTime(2026, 6, 8));

        schedule.NextDueDate.Should().Be(new DateTime(2026, 6, 15));
        schedule.IsActive.Should().BeTrue();
    }

    [Fact]
    public void AdvanceNextDueDateAfter_PastEndDate_DeactivatesSchedule()
    {
        var schedule = CreateSchedule(
            RecurrenceFrequency.Weekly,
            new DateTime(2026, 6, 1),
            endDate: new DateTime(2026, 6, 10));

        schedule.AdvanceNextDueDateAfter(new DateTime(2026, 6, 8));

        schedule.NextDueDate.Should().Be(new DateTime(2026, 6, 15));
        schedule.IsActive.Should().BeFalse();
    }

    // --- Pause / Resume ---

    [Fact]
    public void Pause_SetsInactive()
    {
        var schedule = CreateSchedule(RecurrenceFrequency.Monthly, new DateTime(2026, 6, 1));

        schedule.Pause();

        schedule.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Resume_WithPastDueDate_FastForwardsPastToday()
    {
        var schedule = CreateSchedule(RecurrenceFrequency.Weekly, new DateTime(2026, 6, 1));
        schedule.Pause();

        schedule.Resume(new DateTime(2026, 6, 20));

        schedule.IsActive.Should().BeTrue();
        schedule.NextDueDate.Should().Be(new DateTime(2026, 6, 22));
    }

    [Fact]
    public void Resume_WithFutureDueDate_KeepsNextDueDate()
    {
        var schedule = CreateSchedule(RecurrenceFrequency.Weekly, new DateTime(2026, 7, 1));
        schedule.Pause();

        schedule.Resume(new DateTime(2026, 6, 20));

        schedule.IsActive.Should().BeTrue();
        schedule.NextDueDate.Should().Be(new DateTime(2026, 7, 1));
    }

    [Fact]
    public void Resume_PastEndDate_StaysInactive()
    {
        var schedule = CreateSchedule(
            RecurrenceFrequency.Weekly,
            new DateTime(2026, 6, 1),
            endDate: new DateTime(2026, 6, 10));
        schedule.Pause();

        schedule.Resume(new DateTime(2026, 6, 20));

        schedule.IsActive.Should().BeFalse();
    }
}
