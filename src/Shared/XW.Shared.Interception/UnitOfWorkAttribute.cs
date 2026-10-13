namespace XW.Shared.Interception;

/// <summary>
/// 标记 Application Service 方法需要事务包裹。
/// 由 Castle 拦截器在方法执行前后自动 Begin / Commit / Rollback。
/// ModuleScope 为 None 时走单 DbContext 事务；否则走跨模块事务。
/// Distributed 仅对单 DbContext 事务有效，表示与 CAP Outbox 共享事务。
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class UnitOfWorkAttribute : Attribute
{
    /// <summary>是否与 CAP 消息 Outbox 共享事务。仅单 DbContext 事务路径有效。</summary>
    public bool Distributed { get; set; }

    /// <summary>跨模块事务范围。None 表示走单 DbContext 事务。</summary>
    public ModuleUnitOfWorkScope ModuleScope { get; set; } = ModuleUnitOfWorkScope.None;
}
