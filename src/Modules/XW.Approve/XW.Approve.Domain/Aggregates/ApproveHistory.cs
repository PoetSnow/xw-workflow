using XW.Shared.Domain.Entities;

namespace XW.Approve.Domain.Aggregates;

/// <summary>审批操作历史。聚合内实体，对应 appr_history。</summary>
public class ApproveHistory : DomainEntity
{
    /// <summary>操作类型最大长度。</summary>
    public static readonly int ActionMaxLength = 32;

    /// <summary>意见最大长度。</summary>
    public static readonly int CommentMaxLength = 2000;

    /// <summary>所属实例 Id。</summary>
    public long InstanceId { get; private set; }

    /// <summary>当时所在实例节点 Id。</summary>
    public long? InstanceNodeId { get; private set; }

    /// <summary>关联任务 Id。</summary>
    public long? TaskId { get; private set; }

    /// <summary>操作人，对应 sys_user.id。</summary>
    public long OperatorUserId { get; private set; }

    /// <summary>操作类型，见 ApproveHistoryAction。</summary>
    public string Action { get; private set; } = string.Empty;

    /// <summary>意见。</summary>
    public string Comment { get; private set; } = string.Empty;

    /// <summary>当时表单或节点审计快照。</summary>
    public string? Snapshot { get; private set; }

    /// <summary>操作时间。</summary>
    public DateTime OperateTime { get; private set; } = DateTime.Now;

    private ApproveHistory()
    {
    }

    /// <summary>追加一条历史。id 由调用方生成。</summary>
    internal static ApproveHistory Append(
        long id,
        long instanceId,
        long operatorUserId,
        string action,
        string? comment,
        long? instanceNodeId = null,
        long? taskId = null,
        string? snapshot = null)
    {
        return new ApproveHistory
        {
            Id = id,
            InstanceId = instanceId,
            InstanceNodeId = instanceNodeId,
            TaskId = taskId,
            OperatorUserId = operatorUserId,
            Action = action,
            Comment = comment ?? string.Empty,
            Snapshot = snapshot,
            OperateTime = DateTime.Now,
        };
    }
}
