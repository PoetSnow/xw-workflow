using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using XW.Approve.Infrastructure.Persistence;
using XW.Infra.Repository;
using XW.Shared.Interception;

namespace XW.Host.API.Infrastructure;

/// <summary>
/// MySQL 同库多 DbContext 事务实现。
/// <para>
/// 流程：打开独立 <see cref="MySqlConnection"/> → <c>BEGIN</c> → 对各模块 Context 执行
/// <c>SetDbConnection</c> + <c>UseTransaction</c>，使各 Repository 的 SaveChanges 落入同一物理事务。
/// </para>
/// </summary>
internal sealed class MySqlModuleUnitOfWork(
    ApproveDbContext approveDbContext,
    ILogger<MySqlModuleUnitOfWork> logger) : IModuleUnitOfWork
{
    private MySqlConnection? _connection;
    private DbTransaction? _transaction;
    private ModuleUnitOfWorkScope _scope;

    /// <inheritdoc />
    public bool IsActive => _transaction is not null;

    /// <inheritdoc />
    public async Task BeginAsync(
        ModuleUnitOfWorkScope scope,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        if (IsActive)
        {
            throw new InvalidOperationException("模块事务已开始，不可重复 Begin。");
        }

        if (scope is ModuleUnitOfWorkScope.None)
        {
            throw new ArgumentOutOfRangeException(nameof(scope), "ModuleScope 无效");
        }

        var connectionString = EnsurePomeloConnectionString(
            approveDbContext.Database.GetConnectionString()
            ?? approveDbContext.Database.GetConnectionString()
            ?? throw new InvalidOperationException("无法解析 MySQL 连接字符串"));

        _connection = new MySqlConnection(connectionString);
        await _connection.OpenAsync(cancellationToken);
        _transaction = await _connection.BeginTransactionAsync(isolationLevel, cancellationToken);
        _scope = scope;

        switch (scope)
        {
            //case ModuleUnitOfWorkScope.ManufacturingWithSupplyChain:
            //    await EnlistContextAsync(manufacturingDbContext, cancellationToken);
            //    await EnlistContextAsync(supplyChainDbContext, cancellationToken);
            //    break;
            //case ModuleUnitOfWorkScope.ManufacturingWithSupplyChainAndDevices:
            //    await EnlistContextAsync(manufacturingDbContext, cancellationToken);
            //    await EnlistContextAsync(supplyChainDbContext, cancellationToken);
            //    await EnlistContextAsync(deviceDbContext, cancellationToken);
            //    break;
            //case ModuleUnitOfWorkScope.SupplyChainWithSalesAndDevices:
            //    await EnlistContextAsync(supplyChainDbContext, cancellationToken);
            //    await EnlistContextAsync(salesDbContext, cancellationToken);
            //    await EnlistContextAsync(deviceDbContext, cancellationToken);
            //    break;
            //case ModuleUnitOfWorkScope.SupplyChainWithApprove:
            //    await EnlistContextAsync(supplyChainDbContext, cancellationToken);
            //    await EnlistContextAsync(approveDbContext, cancellationToken);
            //    break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scope), scope, "未支持的模块事务范围");
        }

        logger.LogDebug(
            "Begin module transaction, scope={Scope}, isolation={IsolationLevel}",
            scope,
            isolationLevel);
    }

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            throw new InvalidOperationException("模块事务未开始，无法 Commit。");
        }

        await _transaction.CommitAsync(cancellationToken);
        logger.LogDebug("Commit module transaction, scope={Scope}", _scope);
        await ReleaseAsync();
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.RollbackAsync(cancellationToken);
        logger.LogDebug("Rollback module transaction, scope={Scope}", _scope);
        await ReleaseAsync();
    }

    /// <summary>
    /// 若事务仍未提交/回滚，Dispose 时尝试回滚并释放连接，防止连接泄漏。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            try
            {
                await _transaction.RollbackAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Dispose 时回滚模块事务失败");
            }
        }

        await ReleaseAsync();
    }

    /// <summary>
    /// 将单个 EF Core DbContext 挂入当前共享 ADO 事务。
    /// </summary>
    /// <param name="context">待 enlist 的模块 DbContext。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <exception cref="InvalidOperationException">该 Context 已存在独立活动事务。</exception>
    private async Task EnlistContextAsync(DbContext context, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(
                $"DbContext {context.GetType().Name} 已存在活动事务，无法 enlist 到模块事务。");
        }

        context.Database.SetDbConnection(_connection!);
        await context.Database.UseTransactionAsync(_transaction!, cancellationToken);
    }

    /// <summary>
    /// Pomelo 要求连接串带上这两个开关，否则 UPDATE 的 affected rows 按“实际改动行”算，未改动会被当成并发冲突。
    /// </summary>
    private static string EnsurePomeloConnectionString(string connectionString)
    {
        if (connectionString.Contains("AllowUserVariables", StringComparison.OrdinalIgnoreCase) is false)
        {
            connectionString += ";AllowUserVariables=True";
        }

        if (connectionString.Contains("UseAffectedRows", StringComparison.OrdinalIgnoreCase) is false)
        {
            connectionString += ";UseAffectedRows=False";
        }

        return connectionString;
    }

    /// <summary>
    /// 释放 ADO 事务与连接，并重置 <see cref="_scope"/>。
    /// </summary>
    private async Task ReleaseAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }

        _scope = ModuleUnitOfWorkScope.None;
    }
}
