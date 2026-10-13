using XW.Approve.Domain.Aggregates;

namespace XW.Approve.Domain.Queries;

/// <summary>待办列表行：任务 + 所属实例 + 当前节点名称。</summary>
public sealed class ApproveTaskInboxItem
{
    /// <summary>待办任务。</summary>
    public required ApproveTask Task { get; init; }

    /// <summary>所属实例。</summary>
    public required ApproveInstance Instance { get; init; }

    /// <summary>任务所在实例节点。</summary>
    public ApproveInstanceNode? Node { get; init; }
}
