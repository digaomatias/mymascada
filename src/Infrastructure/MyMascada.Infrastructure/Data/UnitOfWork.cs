using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Infrastructure.Data;

/// <summary>
/// EF Core-backed implementation of <see cref="IUnitOfWork"/>. Wraps an
/// <see cref="IDbContextTransaction"/> so application-layer handlers can compose
/// atomic multi-repository writes without referencing EF Core types directly.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UnitOfWork> _logger;

    public UnitOfWork(ApplicationDbContext context, ILogger<UnitOfWork> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        return new UnitOfWorkTransaction(transaction, _logger);
    }

    private sealed class UnitOfWorkTransaction : IUnitOfWorkTransaction
    {
        private readonly IDbContextTransaction _transaction;
        private readonly ILogger _logger;
        private bool _committed;

        public UnitOfWorkTransaction(IDbContextTransaction transaction, ILogger logger)
        {
            _transaction = transaction;
            _logger = logger;
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await _transaction.CommitAsync(cancellationToken);
            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_committed)
            {
                try
                {
                    await _transaction.RollbackAsync();
                }
                catch (Exception ex)
                {
                    // Don't throw out of DisposeAsync — but make the failed cleanup visible
                    // during incidents. The underlying transaction may already have been
                    // rolled back (e.g. connection drop), which is fine.
                    _logger.LogWarning(ex, "UnitOfWork rollback failed during dispose");
                }
            }

            await _transaction.DisposeAsync();
        }
    }
}
