using XW.Approve.Domain.Models;

namespace XW.Approve.Domain.Repositories;

/// <summary>加载已发布流程版本图。Application 组装后交给聚合，聚合不读库。</summary>
public interface IApproveProcessGraphRepository
{
    /// <summary>按流程定义版本 Id 加载节点、连线、条件、解析器。在途实例用，不要求 Active。</summary>
    Task<ProcessRuntimeGraph?> GetByProcessDefIdAsync(long processDefId, CancellationToken cancellationToken = default);

    /// <summary>仅当流程定义为 Active 时加载图，供发起使用。</summary>
    Task<ProcessRuntimeGraph?> GetActiveByProcessDefIdAsync(long processDefId, CancellationToken cancellationToken = default);
}
