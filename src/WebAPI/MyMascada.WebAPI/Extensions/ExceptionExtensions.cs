using Npgsql;

namespace MyMascada.WebAPI.Extensions;

/// <summary>
/// Helpers for classifying exceptions surfaced by the data layer.
/// </summary>
public static class ExceptionExtensions
{
    /// <summary>
    /// True when the exception (or any inner exception) is a PostgreSQL serialization
    /// failure (SQLSTATE 40001). These are raised when a Serializable transaction loses
    /// a race with a concurrent write (e.g. two split replacements, or a split
    /// replacement racing an amount edit) and are safe for the client to retry.
    /// EF wraps them in <see cref="Microsoft.EntityFrameworkCore.DbUpdateException"/>
    /// at SaveChanges time; at Commit time they can surface as a bare
    /// <see cref="PostgresException"/> — hence the inner-exception walk.
    /// </summary>
    public static bool IsPostgresSerializationFailure(this Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is PostgresException postgres &&
                postgres.SqlState == PostgresErrorCodes.SerializationFailure)
            {
                return true;
            }
        }

        return false;
    }
}
