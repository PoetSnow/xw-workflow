namespace XW.Infra.Repository;

/// <summary>
/// 单体同库场景下，跨限界上下文 DbContext 共享事务的范围。
/// <para>
/// 在 Contracts 接口方法上通过 <see cref="Interceptor.UnitOfWorkAttribute.ModuleScope"/> 指定；
/// 由 <see cref="IModuleUnitOfWork"/> 实现按范围 enlist 对应 DbContext。
/// </para>
/// </summary>
public enum ModuleUnitOfWorkScope
{
    /// <summary>
    /// 不使用模块事务；走单 DbContext 的 <see cref="IUnitOfWork"/>（或未开事务）。
    /// </summary>
    None = 0,

    /// <summary>
    /// Manufacturing + SupplyChain。
    /// <para>典型用例：生产领料过账、生产退料过账、Unit 绑定 SN（改 SN 状态 + 写组件）。</para>
    /// </summary>
    ManufacturingWithSupplyChain = 1,

    /// <summary>
    /// Manufacturing + SupplyChain + Devices。
    /// <para>典型用例：成品批量入库（SC 生产入库 + Mfg Unit/工单 + Devices 设备档案）。</para>
    /// </summary>
    ManufacturingWithSupplyChainAndDevices = 2,

    /// <summary>
    /// SupplyChain + Sales + Devices。
    /// <para>典型用例：销售出库过账（扣库存/SN + 回写销售发货量 + 设备开通）。</para>
    /// </summary>
    SupplyChainWithSalesAndDevices = 3,

    /// <summary>
    /// SupplyChain + Approve。
    /// <para>典型用例：采购单提交审批、审批通过/驳回回写单据。</para>
    /// </summary>
    SupplyChainWithApprove = 4,
}
