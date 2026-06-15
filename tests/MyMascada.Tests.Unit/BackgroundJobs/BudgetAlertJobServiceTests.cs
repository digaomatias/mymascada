using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Infrastructure.BackgroundJobs;
using NSubstitute.ExceptionExtensions;

namespace MyMascada.Tests.Unit.BackgroundJobs;

public class BudgetAlertJobServiceTests
{
    private readonly IBudgetRepository _budgetRepo;
    private readonly INotificationTriggerService _triggerService;
    private readonly BudgetAlertJobService _sut;

    public BudgetAlertJobServiceTests()
    {
        _budgetRepo = Substitute.For<IBudgetRepository>();
        _triggerService = Substitute.For<INotificationTriggerService>();

        // Wire a scope-factory chain that resolves our two mocked services.
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(IBudgetRepository)).Returns(_budgetRepo);
        provider.GetService(typeof(INotificationTriggerService)).Returns(_triggerService);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(provider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        _sut = new BudgetAlertJobService(scopeFactory, Substitute.For<ILogger<BudgetAlertJobService>>());
    }

    [Fact]
    public async Task ProcessAllUsers_CallsTriggerOncePerUserWithActiveBudget()
    {
        var u1 = Guid.NewGuid();
        var u2 = Guid.NewGuid();
        _budgetRepo.GetUserIdsWithActiveBudgetsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { u1, u2 });

        await _sut.ProcessAllUsersAsync();

        await _triggerService.Received(1).CheckBudgetThresholdsAsync(u1, Arg.Any<CancellationToken>());
        await _triggerService.Received(1).CheckBudgetThresholdsAsync(u2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAllUsers_NoUsers_DoesNotCallTrigger()
    {
        _budgetRepo.GetUserIdsWithActiveBudgetsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Guid>());

        await _sut.ProcessAllUsersAsync();

        await _triggerService.DidNotReceive().CheckBudgetThresholdsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAllUsers_PerUserErrorDoesNotAbortOtherUsers()
    {
        var failing = Guid.NewGuid();
        var ok = Guid.NewGuid();
        _budgetRepo.GetUserIdsWithActiveBudgetsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { failing, ok });
        _triggerService.CheckBudgetThresholdsAsync(failing, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        // The job aggregates per-user failures and rethrows so Hangfire records a
        // failed run, but every user must still be attempted first.
        var act = () => _sut.ProcessAllUsersAsync();
        await act.Should().ThrowAsync<AggregateException>();

        await _triggerService.Received(1).CheckBudgetThresholdsAsync(ok, Arg.Any<CancellationToken>());
    }
}
