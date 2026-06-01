namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Lightweight unit-of-work abstraction so application-layer handlers can compose
/// multiple repository writes into a single atomic database transaction without
/// taking a hard dependency on EF Core types.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Begins a database transaction. The caller must Commit or the transaction will
    /// be rolled back when the returned scope is disposed.
    /// </summary>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A transactional scope. Disposing without committing rolls back.
/// </summary>
public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    /// <summary>
    /// Commits the underlying database transaction.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);
}
