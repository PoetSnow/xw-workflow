using DotNetCore.CAP;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using XW.Infra.Repository.EfCore.Transaction;
using System.Data;

namespace XW.Infra.Repository.EfCore.SqlServer.Transaction;

public class SqlServerUnitOfWork<TDbContext>(TDbContext context, ILogger<SqlServerUnitOfWork<TDbContext>>? logger, ICapPublisher? publisher = null) : UnitOfWork<TDbContext>(context, logger)
    where TDbContext : SqlServerDbContext
{
    private readonly ICapPublisher? _publisher = publisher;

    protected override IDbContextTransaction GetDbContextTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, bool distributed = false)
    {
        if (distributed && _publisher is not null)
        {
            return XWDbContext.Database.BeginTransaction(isolationLevel, _publisher, false);
        }

        if (distributed && _publisher is null)
        {
            logger?.LogWarning(
                "CapPublisher 未配置，Distributed UnitOfWork 已降级为本地事务（DbContext={DbContext}）",
                typeof(TDbContext).Name);
        }

        return XWDbContext.Database.BeginTransaction(isolationLevel);
    }
}
