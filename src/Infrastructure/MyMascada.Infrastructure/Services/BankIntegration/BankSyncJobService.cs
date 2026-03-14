using System.Collections.Concurrent;
using Hangfire;
using Hangfire.Server;
using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Events;
using MyMascada.Application.Features.BankConnections.DTOs;
using MyMascada.Domain.Enums;

namespace MyMascada.Infrastructure.Services.BankIntegration;

/// <summary>
/// Enqueues and tracks user-triggered bank sync jobs so the API can respond immediately.
/// </summary>
public class BankSyncJobService : IBankSyncJobService
{
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IBankSyncService _bankSyncService;
    private readonly IMediator _mediator;
    private readonly IApplicationLogger<BankSyncJobService> _logger;
    private readonly InMemoryBankSyncJobTracker _tracker;

    public BankSyncJobService(
        IBackgroundJobClient backgroundJobClient,
        IBankSyncService bankSyncService,
        IMediator mediator,
        IApplicationLogger<BankSyncJobService> logger,
        InMemoryBankSyncJobTracker tracker)
    {
        _backgroundJobClient = backgroundJobClient ?? throw new ArgumentNullException(nameof(backgroundJobClient));
        _bankSyncService = bankSyncService ?? throw new ArgumentNullException(nameof(bankSyncService));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
    }

    public BankSyncJobAcceptedDto EnqueueConnectionSync(Guid userId, int connectionId)
    {
        var trackingId = Guid.NewGuid().ToString("N");
        var accepted = _tracker.Register(trackingId, userId, "connection", new[] { connectionId });

        _backgroundJobClient.Enqueue<BankSyncJobService>(
            service => service.ProcessConnectionSyncJobAsync(trackingId, connectionId, userId, null));

        _logger.LogInformation(
            "Enqueued bank sync job {TrackingId} for connection {ConnectionId} and user {UserId}",
            trackingId, connectionId, userId);

        return accepted;
    }

    public BankSyncJobAcceptedDto EnqueueAllConnectionsSync(Guid userId, IReadOnlyCollection<int> connectionIds)
    {
        var normalizedConnectionIds = connectionIds.Distinct().ToArray();
        var trackingId = Guid.NewGuid().ToString("N");
        var accepted = _tracker.Register(trackingId, userId, "all", normalizedConnectionIds);

        _backgroundJobClient.Enqueue<BankSyncJobService>(
            service => service.ProcessAllConnectionsSyncJobAsync(trackingId, userId, normalizedConnectionIds, null));

        _logger.LogInformation(
            "Enqueued bank sync-all job {TrackingId} for {ConnectionCount} connections and user {UserId}",
            trackingId, normalizedConnectionIds.Length, userId);

        return accepted;
    }

    public BankSyncJobStatusDto GetStatus(string jobId, Guid userId)
    {
        return _tracker.GetStatus(jobId, userId);
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task ProcessConnectionSyncJobAsync(
        string trackingId,
        int connectionId,
        Guid userId,
        PerformContext? performContext = null)
    {
        _tracker.MarkRunning(trackingId);

        try
        {
            var result = await _bankSyncService.SyncAccountAsync(connectionId, BankSyncType.Manual);

            if (result.IsSuccess && result.ImportedTransactionIds.Any())
            {
                await _mediator.Publish(
                    new TransactionsCreatedEvent(result.ImportedTransactionIds, userId));
            }

            _tracker.RecordConnectionResult(trackingId, result);
            _tracker.MarkCompleted(trackingId, result.IsSuccess ? BankSyncJobTerminalStatus.Succeeded : BankSyncJobTerminalStatus.Failed);
        }
        catch (Exception ex)
        {
            _tracker.MarkFailed(trackingId, "The sync failed. Please try again.");
            _logger.LogError(ex, "Background bank sync job {TrackingId} failed for connection {ConnectionId}", trackingId, connectionId);
            throw;
        }
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task ProcessAllConnectionsSyncJobAsync(
        string trackingId,
        Guid userId,
        IReadOnlyCollection<int> connectionIds,
        PerformContext? performContext = null)
    {
        _tracker.MarkRunning(trackingId);

        var hadErrors = false;

        foreach (var connectionId in connectionIds)
        {
            try
            {
                var result = await _bankSyncService.SyncAccountAsync(connectionId, BankSyncType.Manual);

                if (result.IsSuccess && result.ImportedTransactionIds.Any())
                {
                    await _mediator.Publish(
                        new TransactionsCreatedEvent(result.ImportedTransactionIds, userId));
                }

                _tracker.RecordConnectionResult(trackingId, result);

                if (!result.IsSuccess)
                {
                    hadErrors = true;
                }
            }
            catch (Exception ex)
            {
                hadErrors = true;
                _tracker.RecordUnhandledConnectionFailure(trackingId, connectionId, "The sync failed for this connection.");
                _logger.LogError(
                    ex,
                    "Background sync-all job {TrackingId} failed while processing connection {ConnectionId}",
                    trackingId,
                    connectionId);
            }
        }

        _tracker.MarkCompleted(
            trackingId,
            hadErrors ? BankSyncJobTerminalStatus.CompletedWithErrors : BankSyncJobTerminalStatus.Succeeded);
    }
}

public sealed class InMemoryBankSyncJobTracker
{
    private static readonly TimeSpan Retention = TimeSpan.FromHours(6);
    private readonly ConcurrentDictionary<string, BankSyncJobState> _jobs = new();

    public BankSyncJobAcceptedDto Register(string jobId, Guid userId, string scope, IReadOnlyCollection<int> connectionIds)
    {
        var state = new BankSyncJobState
        {
            JobId = jobId,
            UserId = userId,
            Scope = scope,
            ConnectionIds = connectionIds.Distinct().ToArray(),
            StartedAt = DateTime.UtcNow,
            TotalConnections = connectionIds.Count,
            Status = "queued"
        };

        _jobs[jobId] = state;
        CleanupExpired();

        return new BankSyncJobAcceptedDto
        {
            JobId = state.JobId,
            Scope = state.Scope,
            StartedAt = state.StartedAt,
            ConnectionIds = state.ConnectionIds,
            TotalConnections = state.TotalConnections
        };
    }

    public BankSyncJobStatusDto GetStatus(string jobId, Guid userId)
    {
        if (!_jobs.TryGetValue(jobId, out var state) || state.UserId != userId)
        {
            throw new ArgumentException($"Bank sync job {jobId} not found");
        }

        lock (state.SyncRoot)
        {
            return new BankSyncJobStatusDto
            {
                JobId = state.JobId,
                Scope = state.Scope,
                Status = state.Status,
                StartedAt = state.StartedAt,
                CompletedAt = state.CompletedAt,
                ConnectionIds = state.ConnectionIds,
                TotalConnections = state.TotalConnections,
                CompletedConnections = state.CompletedConnections,
                FailedConnections = state.FailedConnections,
                TransactionsImported = state.TransactionsImported,
                TransactionsSkipped = state.TransactionsSkipped,
                ErrorMessage = state.ErrorMessage
            };
        }
    }

    public void MarkRunning(string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
            return;

        lock (state.SyncRoot)
        {
            state.Status = "processing";
            state.LastUpdatedAt = DateTime.UtcNow;
        }
    }

    public void RecordConnectionResult(string jobId, BankSyncResult result)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
            return;

        lock (state.SyncRoot)
        {
            state.CompletedConnections++;
            state.TransactionsImported += result.TransactionsImported;
            state.TransactionsSkipped += result.TransactionsSkipped;
            state.LastUpdatedAt = DateTime.UtcNow;

            if (!result.IsSuccess)
            {
                state.FailedConnections++;
                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    state.ErrorMessage = AppendError(state.ErrorMessage, result.ErrorMessage);
                }
            }
        }
    }

    public void RecordUnhandledConnectionFailure(string jobId, int connectionId, string errorMessage)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
            return;

        lock (state.SyncRoot)
        {
            state.CompletedConnections++;
            state.FailedConnections++;
            state.LastUpdatedAt = DateTime.UtcNow;
            state.ErrorMessage = AppendError(
                state.ErrorMessage,
                $"Connection {connectionId}: {errorMessage}");
        }
    }

    public void MarkCompleted(string jobId, BankSyncJobTerminalStatus terminalStatus)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
            return;

        lock (state.SyncRoot)
        {
            state.Status = terminalStatus switch
            {
                BankSyncJobTerminalStatus.Succeeded => "succeeded",
                BankSyncJobTerminalStatus.CompletedWithErrors => "completed_with_errors",
                _ => "failed"
            };
            state.CompletedAt = DateTime.UtcNow;
            state.LastUpdatedAt = state.CompletedAt.Value;
        }
    }

    public void MarkFailed(string jobId, string errorMessage)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
            return;

        lock (state.SyncRoot)
        {
            state.Status = "failed";
            state.ErrorMessage = AppendError(state.ErrorMessage, errorMessage);
            state.CompletedAt = DateTime.UtcNow;
            state.LastUpdatedAt = state.CompletedAt.Value;
            state.CompletedConnections = state.TotalConnections == 0 ? 0 : Math.Max(state.CompletedConnections, 1);
            state.FailedConnections = state.TotalConnections == 0 ? 0 : Math.Max(state.FailedConnections, 1);
        }
    }

    private void CleanupExpired()
    {
        var cutoff = DateTime.UtcNow - Retention;
        foreach (var (jobId, state) in _jobs)
        {
            if (state.LastUpdatedAt < cutoff)
            {
                _jobs.TryRemove(jobId, out _);
            }
        }
    }

    private static string AppendError(string? current, string next)
    {
        return string.IsNullOrWhiteSpace(current) ? next : $"{current}; {next}";
    }
}

internal sealed class BankSyncJobState
{
    public object SyncRoot { get; } = new();
    public string JobId { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Scope { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "queued";
    public IReadOnlyList<int> ConnectionIds { get; set; } = Array.Empty<int>();
    public int TotalConnections { get; set; }
    public int CompletedConnections { get; set; }
    public int FailedConnections { get; set; }
    public int TransactionsImported { get; set; }
    public int TransactionsSkipped { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum BankSyncJobTerminalStatus
{
    Succeeded,
    CompletedWithErrors,
    Failed
}
