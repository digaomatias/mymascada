using Microsoft.EntityFrameworkCore;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Data;
using MyMascada.Infrastructure.Repositories;

namespace MyMascada.Tests.Unit.Repositories;

public class RecurringTransactionRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly RecurringTransactionRepository _repository;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();
    private int _accountId;

    public RecurringTransactionRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _repository = new RecurringTransactionRepository(_context);

        var account = new Account { Name = "Checking", UserId = _userId };
        _context.Accounts.Add(account);
        _context.SaveChanges();
        _accountId = account.Id;
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private RecurringTransaction NewSchedule(
        Guid? userId = null,
        bool isActive = true,
        DateTime? nextDueDate = null)
    {
        return new RecurringTransaction
        {
            UserId = userId ?? _userId,
            AccountId = _accountId,
            Description = "Gym membership",
            Amount = 25m,
            Frequency = RecurrenceFrequency.Monthly,
            StartDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            NextDueDate = nextDueDate ?? new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
            IsActive = isActive
        };
    }

    [Fact]
    public async Task CreateAsync_PersistsSchedule()
    {
        var created = await _repository.CreateAsync(NewSchedule());

        created.Id.Should().BeGreaterThan(0);
        (await _context.RecurringTransactions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GetByIdAsync_WrongUser_ReturnsNull()
    {
        var created = await _repository.CreateAsync(NewSchedule());

        var result = await _repository.GetByIdAsync(created.Id, _otherUserId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserIdAsync_ExcludesInactiveByDefault()
    {
        await _repository.CreateAsync(NewSchedule(isActive: true));
        await _repository.CreateAsync(NewSchedule(isActive: false));

        var activeOnly = await _repository.GetByUserIdAsync(_userId);
        var all = await _repository.GetByUserIdAsync(_userId, includeInactive: true);

        activeOnly.Should().HaveCount(1);
        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetDueAsync_ReturnsOnlyActiveDueSchedules()
    {
        var asOf = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var dueToday = await _repository.CreateAsync(NewSchedule(nextDueDate: asOf));
        await _repository.CreateAsync(NewSchedule(nextDueDate: asOf.AddDays(1))); // future
        await _repository.CreateAsync(NewSchedule(nextDueDate: asOf, isActive: false)); // paused

        var due = (await _repository.GetDueAsync(asOf)).ToList();

        due.Should().ContainSingle().Which.Id.Should().Be(dueToday.Id);
    }

    [Fact]
    public async Task GetDueAsync_IncludesOverdueSchedules()
    {
        var asOf = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        await _repository.CreateAsync(NewSchedule(nextDueDate: asOf.AddDays(-10)));

        var due = await _repository.GetDueAsync(asOf);

        due.Should().HaveCount(1);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletes()
    {
        var created = await _repository.CreateAsync(NewSchedule());

        var deleted = await _repository.DeleteAsync(created.Id, _userId);

        deleted.Should().BeTrue();
        (await _repository.GetByIdAsync(created.Id, _userId)).Should().BeNull();

        var raw = await _context.RecurringTransactions
            .IgnoreQueryFilters()
            .SingleAsync(r => r.Id == created.Id);
        raw.IsDeleted.Should().BeTrue();
        raw.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_WrongUser_ReturnsFalse()
    {
        var created = await _repository.CreateAsync(NewSchedule());

        var deleted = await _repository.DeleteAsync(created.Id, _otherUserId);

        deleted.Should().BeFalse();
    }

    [Fact]
    public async Task HasOccurrenceAsync_DetectsExistingScheduledDate()
    {
        var created = await _repository.CreateAsync(NewSchedule());
        var date = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        (await _repository.HasOccurrenceAsync(created.Id, date)).Should().BeFalse();

        await _repository.CreateOccurrenceAsync(new RecurringTransactionOccurrence
        {
            RecurringTransactionId = created.Id,
            ScheduledDate = date,
            Status = RecurringTransactionOccurrenceStatus.Notified
        });

        (await _repository.HasOccurrenceAsync(created.Id, date)).Should().BeTrue();
        (await _repository.HasOccurrenceAsync(created.Id, date.AddDays(1))).Should().BeFalse();
    }

    [Fact]
    public async Task GetRecentOccurrencesAsync_ReturnsNewestFirstLimited()
    {
        var created = await _repository.CreateAsync(NewSchedule());
        var baseDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < 5; i++)
        {
            await _repository.CreateOccurrenceAsync(new RecurringTransactionOccurrence
            {
                RecurringTransactionId = created.Id,
                ScheduledDate = baseDate.AddMonths(i),
                Status = RecurringTransactionOccurrenceStatus.Notified
            });
        }

        var recent = (await _repository.GetRecentOccurrencesAsync(created.Id, _userId, count: 3)).ToList();

        recent.Should().HaveCount(3);
        recent.First().ScheduledDate.Should().Be(baseDate.AddMonths(4));
        recent.Should().BeInDescendingOrder(o => o.ScheduledDate);
    }

    [Fact]
    public async Task GetRecentOccurrencesAsync_WrongUser_ReturnsEmpty()
    {
        var created = await _repository.CreateAsync(NewSchedule());
        await _repository.CreateOccurrenceAsync(new RecurringTransactionOccurrence
        {
            RecurringTransactionId = created.Id,
            ScheduledDate = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
            Status = RecurringTransactionOccurrenceStatus.Notified
        });

        var recent = await _repository.GetRecentOccurrencesAsync(created.Id, _otherUserId);

        recent.Should().BeEmpty();
    }
}
