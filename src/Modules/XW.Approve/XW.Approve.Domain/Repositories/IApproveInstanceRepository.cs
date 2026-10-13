using XW.Approve.Domain.Aggregates;

namespace XW.Approve.Domain.Repositories;

/// <summary>审批实例聚合仓储。Application 通过本接口加载和持久化，不直接依赖 EF。</summary>
public interface IApproveInstanceRepository
{
    /// <summary>按 Id 加载实例及子实体，供写操作使用。</summary>
    Task<ApproveInstance?> GetForWriteAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>按 Id 加载实例及子实体，供详情只读。</summary>
    Task<ApproveInstance?> GetWithChildrenAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>查找同一业务进行中的实例。</summary>
    Task<ApproveInstance?> FindRunningAsync(string businessType, string businessId, CancellationToken cancellationToken = default);

    /// <summary>同一业务最近一条实例，含已结束的。</summary>
    Task<ApproveInstance?> FindLatestByBusinessAsync(string businessType, string businessId, CancellationToken cancellationToken = default);

    /// <summary>同一业务的全部实例，按提交时间从早到晚，含驳回和撤销。</summary>
    Task<IReadOnlyList<ApproveInstance>> ListByBusinessAsync(string businessType, string businessId, CancellationToken cancellationToken = default);

    /// <summary>新增实例。</summary>
    Task InsertAsync(ApproveInstance instance, CancellationToken cancellationToken = default);

    /// <summary>更新实例及其子实体。</summary>
    Task UpdateAsync(ApproveInstance instance, CancellationToken cancellationToken = default);

    /// <summary>按任务 Id 加载所属实例（含子实体），供通过/驳回。</summary>
    Task<ApproveInstance?> GetByTaskIdForWriteAsync(long taskId, CancellationToken cancellationToken = default);
}
