namespace XW.Shared.Interception;

/// <summary>
/// 单体同库场景下，跨限界上下文 DbContext 共享事务的范围。
/// 在契约接口方法上通过 <see cref="UnitOfWorkAttribute.ModuleScope"/> 指定。
/// </summary>
public enum ModuleUnitOfWorkScope
{
    /// <summary>不使用模块事务，走单 DbContext 事务。</summary>
    None = 0,

    /// <summary>Manufacturing + SupplyChain。</summary>
    ManufacturingWithSupplyChain = 1,

    /// <summary>Manufacturing + SupplyChain + Devices。</summary>
    ManufacturingWithSupplyChainAndDevices = 2,

    /// <summary>SupplyChain + Sales + Devices。</summary>
    SupplyChainWithSalesAndDevices = 3,

    /// <summary>SupplyChain + Approve。</summary>
    SupplyChainWithApprove = 4,
}
