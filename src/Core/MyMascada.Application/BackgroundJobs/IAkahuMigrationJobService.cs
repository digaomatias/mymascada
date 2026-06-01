namespace MyMascada.Application.BackgroundJobs;

/// <summary>
/// Service that enqueues Akahu classic→official migration as a Hangfire background job,
/// keeping the webhook and OAuth callbacks responsive (they must not block on the
/// migration's network calls to Akahu and database remap).
/// </summary>
public interface IAkahuMigrationJobService
{
    /// <summary>
    /// Enqueues a fire-and-forget migration for one connection.
    /// </summary>
    /// <returns>The Hangfire job ID for diagnostics.</returns>
    string EnqueueMigration(Guid userId, int bankConnectionId);

    /// <summary>
    /// Hangfire entry-point. Sends the MigrateAkahuConnectionCommand inside a fresh DI scope.
    /// </summary>
    Task ProcessMigrationAsync(Guid userId, int bankConnectionId);
}
