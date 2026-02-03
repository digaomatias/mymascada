using FluentAssertions;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Infrastructure.BackgroundJobs;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace MyMascada.Tests.Unit.BackgroundJobs;

public class TokenCleanupServiceTests
{
    private readonly IRefreshTokenRepository _rtr;
    private readonly IPasswordResetTokenRepository _ptr;
    private readonly ILogger<TokenCleanupService> _log;
    private readonly TokenCleanupService _svc;

    public TokenCleanupServiceTests()
    {
        _rtr = Substitute.For<IRefreshTokenRepository>();
        _ptr = Substitute.For<IPasswordResetTokenRepository>();
        _log = Substitute.For<ILogger<TokenCleanupService>>();
        _svc = new TokenCleanupService(_rtr, _ptr, _log);
    }

    [Fact]
    public async Task CleanupExpiredRefreshTokensAsync_WhenTokensExist_CallsRepository()
    {
        _rtr.DeleteExpiredAndRevokedTokensAsync(Arg.Any<DateTime>()).Returns(5);
        await _svc.CleanupExpiredRefreshTokensAsync();
        await _rtr.Received(1).DeleteExpiredAndRevokedTokensAsync(Arg.Is<DateTime>(d => d < DateTime.UtcNow && d > DateTime.UtcNow.AddDays(-8)));
    }

    [Fact]
    public async Task CleanupExpiredRefreshTokensAsync_WhenTokensDeleted_LogsInformation()
    {
        _rtr.DeleteExpiredAndRevokedTokensAsync(Arg.Any<DateTime>()).Returns(5);
        await _svc.CleanupExpiredRefreshTokensAsync();
        _log.Received(1).Log(LogLevel.Information, Arg.Any<EventId>(), Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task CleanupExpiredRefreshTokensAsync_WhenNoTokensDeleted_DoesNotLog()
    {
        _rtr.DeleteExpiredAndRevokedTokensAsync(Arg.Any<DateTime>()).Returns(0);
        await _svc.CleanupExpiredRefreshTokensAsync();
        _log.DidNotReceive().Log(LogLevel.Information, Arg.Any<EventId>(), Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task CleanupExpiredRefreshTokensAsync_WhenThrows_DoesNotRethrow()
    {
        _rtr.DeleteExpiredAndRevokedTokensAsync(Arg.Any<DateTime>()).ThrowsAsync(new InvalidOperationException("fail"));
        var act = () => _svc.CleanupExpiredRefreshTokensAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CleanupExpiredRefreshTokensAsync_WhenThrows_LogsError()
    {
        var ex = new InvalidOperationException("fail");
        _rtr.DeleteExpiredAndRevokedTokensAsync(Arg.Any<DateTime>()).ThrowsAsync(ex);
        await _svc.CleanupExpiredRefreshTokensAsync();
        _log.Received(1).Log(LogLevel.Error, Arg.Any<EventId>(), Arg.Any<object>(), Arg.Is<Exception>(e => e == ex), Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task CleanupExpiredPasswordResetTokensAsync_WhenTokensExist_CallsRepository()
    {
        _ptr.DeleteExpiredAndUsedTokensAsync(Arg.Any<DateTime>()).Returns(3);
        await _svc.CleanupExpiredPasswordResetTokensAsync();
        await _ptr.Received(1).DeleteExpiredAndUsedTokensAsync(Arg.Is<DateTime>(d => d < DateTime.UtcNow && d > DateTime.UtcNow.AddDays(-8)));
    }

    [Fact]
    public async Task CleanupExpiredPasswordResetTokensAsync_WhenTokensDeleted_LogsInformation()
    {
        _ptr.DeleteExpiredAndUsedTokensAsync(Arg.Any<DateTime>()).Returns(3);
        await _svc.CleanupExpiredPasswordResetTokensAsync();
        _log.Received(1).Log(LogLevel.Information, Arg.Any<EventId>(), Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task CleanupExpiredPasswordResetTokensAsync_WhenNoTokensDeleted_DoesNotLog()
    {
        _ptr.DeleteExpiredAndUsedTokensAsync(Arg.Any<DateTime>()).Returns(0);
        await _svc.CleanupExpiredPasswordResetTokensAsync();
        _log.DidNotReceive().Log(LogLevel.Information, Arg.Any<EventId>(), Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task CleanupExpiredPasswordResetTokensAsync_WhenThrows_DoesNotRethrow()
    {
        _ptr.DeleteExpiredAndUsedTokensAsync(Arg.Any<DateTime>()).ThrowsAsync(new InvalidOperationException("fail"));
        var act = () => _svc.CleanupExpiredPasswordResetTokensAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CleanupExpiredPasswordResetTokensAsync_WhenThrows_LogsError()
    {
        var ex = new InvalidOperationException("fail");
        _ptr.DeleteExpiredAndUsedTokensAsync(Arg.Any<DateTime>()).ThrowsAsync(ex);
        await _svc.CleanupExpiredPasswordResetTokensAsync();
        _log.Received(1).Log(LogLevel.Error, Arg.Any<EventId>(), Arg.Any<object>(), Arg.Is<Exception>(e => e == ex), Arg.Any<Func<object, Exception?, string>>());
    }
}
