using System.Data;
using XW.Shared.Interception;

namespace XW.Infra.Repository;

/// <summary>
/// 模块化单体（同库）跨限界上下文的事务协调器。
/// <para>
/// 各模块拥有独立 <c>DbContext</c>，但共享同一 MySQL 实例时，可通过本接口将多个 Context
/// enlist 到同一条 ADO 事务，保证跨域写操作（如领料过账 = 更新领料单 + 扣库存）原子提交。
/// </para>
/// </summary>
public interface IModuleUnitOfWork : IAsyncDisposable
{
    /// <summary>
    /// 当前请求作用域内是否已开启模块事务（<c>BeginAsync</c> 之后、<c>Commit/Rollback</c> 之前为 <c>true</c>）。
    /// 用于拦截器识别嵌套 <see cref="UnitOfWorkAttribute"/> 调用，避免重复 Begin/Commit。
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// 按指定范围开启模块事务，并将对应模块的 DbContext 挂入同一条 MySQL 事务。
    /// </summary>
    /// <param name="scope">参与事务的模块组合，见 <see cref="ModuleUnitOfWorkScope"/>。</param>
    /// <param name="isolationLevel">隔离级别，默认 ReadCommitted。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <exception cref="InvalidOperationException">事务已开启时重复调用。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="scope"/> 为 <see cref="ModuleUnitOfWorkScope.None"/>。</exception>
    Task BeginAsync(
        ModuleUnitOfWorkScope scope,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 提交当前模块事务；提交后释放底层连接与事务资源。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <exception cref="InvalidOperationException">尚未调用 <see cref="BeginAsync"/>。</exception>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 回滚当前模块事务；若事务未开启则静默忽略。回滚后释放资源。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
