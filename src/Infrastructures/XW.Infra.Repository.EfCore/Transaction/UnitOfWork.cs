using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System.Data;

namespace XW.Infra.Repository.EfCore.Transaction;

public abstract class UnitOfWork<TDbContext>(TDbContext context, ILogger<UnitOfWork<TDbContext>>? logger) : IUnitOfWork
    where TDbContext : DbContext
{
    protected TDbContext XWDbContext { get; init; } = context ?? throw new ArgumentNullException(nameof(context));

    protected IDbContextTransaction? DbTransaction { get; set; }

    public bool IsStartingUow => XWDbContext.Database.CurrentTransaction is not null;

    public void BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, bool distributed = false)
    {
        if (XWDbContext.Database.CurrentTransaction is not null)
        {
            throw new InvalidDataException($"UnitOfWork Error,{XWDbContext.Database.CurrentTransaction}");
        }
        else
        {
            XWDbContext.Database.AutoSavepointsEnabled = false;
            DbTransaction = GetDbContextTransaction(isolationLevel, distributed);
            logger?.LogDebug("Begin Transaction, transactionId:{transactionId}, IsolationLevel:{IsolationLevel}, Distributed:{Distributed}", DbTransaction.TransactionId, isolationLevel, distributed);
        }
    }

    public void Commit()
    {
        if (DbTransaction is null)
        {
            throw new InvalidDataException($"{nameof(DbTransaction)} is null");
        }
        else
        {
            DbTransaction.Commit();
            logger?.LogDebug("Commit Transaction, transactionId:{transactionId}", DbTransaction.TransactionId);
        }
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (DbTransaction is null)
        {
            throw new InvalidDataException($"{nameof(DbTransaction)} is null");
        }
        else
        {
            await DbTransaction.CommitAsync(cancellationToken);
            logger?.LogDebug("CommitAsync Transaction, transactionId:{transactionId}", DbTransaction.TransactionId);
        }
    }

    public void Rollback()
    {
        if (DbTransaction is null)
        {
            return;
        }

        DbTransaction.Rollback();
        logger?.LogDebug("Rollback Transaction, transactionId:{transactionId}", DbTransaction.TransactionId);
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (DbTransaction is null)
        {
            return;
        }

        await DbTransaction.RollbackAsync(cancellationToken);
        logger?.LogDebug("RollbackAsync Transaction, transactionId:{transactionId}", DbTransaction.TransactionId);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected abstract IDbContextTransaction GetDbContextTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, bool distributed = false);

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (DbTransaction is not null)
            {
                DbTransaction.Dispose();
                DbTransaction = null;
            }
        }
    }
}
