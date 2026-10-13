namespace XW.Infra.Repository.Interceptor;

/// <summary>
/// 标记 Application Service 方法需要事务包裹。
/// <para>
/// <list type="bullet">
/// <item><description>默认：使用 Host 注册的 <see cref="IUnitOfWork"/>（单 DbContext，如 SupplyChain 内部过账）。</description></item>
/// <item><description><see cref="ModuleScope"/> 非 None：使用 <see cref="IModuleUnitOfWork"/>（同库多 DbContext 跨模块写）。</description></item>
/// </list>
/// 由 Castle <c>UowAsyncInterceptor</c> 在方法执行前后自动 Begin / Commit / Rollback。
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class UnitOfWorkAttribute : Attribute
{
    /// <summary>
    /// 是否与 CAP 消息 Outbox 共享事务（仅单 DbContext <see cref="IUnitOfWork"/> 路径有效）。
    /// 模块事务路径暂不支持 CAP 分布式标记。
    /// </summary>
    public bool Distributed { get; set; }

    /// <summary>
    /// 跨模块事务范围。非 <see cref="ModuleUnitOfWorkScope.None"/> 时启用 <see cref="IModuleUnitOfWork"/>，
    /// 此时忽略单 Context 的 <see cref="IUnitOfWork"/>。
    /// </summary>
    public ModuleUnitOfWorkScope ModuleScope { get; set; } = ModuleUnitOfWorkScope.None;
}
