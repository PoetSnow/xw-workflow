using XW.Approve.Domain.Aggregates;

namespace XW.Approve.Domain.Queries;

/// <summary>审批读模型。Application 用来组列表，不走聚合写方法。</summary>
public interface IApproveQuery
{
    /// <summary>当前用户待处理任务。</summary>
    Task<PagedResult<ApproveTaskInboxItem>> GetMyPendingTasksAsync(
        long userId,
        string? keywords,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>当前用户发起的实例。</summary>
    Task<PagedResult<ApproveInstance>> GetMySubmittedAsync(
        long userId,
        string? keywords,
        string? status,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}
