using XW.Approve.Domain.Constants;
using XW.Approve.Domain.Entities;
using XW.Infra.IdGenerater.Yitter;
using XW.Shared.Domain.Entities;

namespace XW.Approve.Domain.Aggregates;

/// <summary>实例节点快照。聚合内实体，对应 appr_instance_node。</summary>
public class ApproveInstanceNode : DomainEntity
{
    /// <summary>节点编码最大长度。</summary>
    public static readonly int NodeKeyMaxLength = 64;

    /// <summary>节点名称最大长度。</summary>
    public static readonly int NodeNameMaxLength = 128;

    /// <summary>状态最大长度。</summary>
    public static readonly int StatusMaxLength = 16;

    /// <summary>所属实例 Id。</summary>
    public long InstanceId { get; private set; }

    /// <summary>来源流程节点 Id。</summary>
    public long ProcessNodeId { get; private set; }

    /// <summary>节点编码快照。</summary>
    public string NodeKey { get; private set; } = string.Empty;

    /// <summary>节点名称快照。</summary>
    public string NodeName { get; private set; } = string.Empty;

    /// <summary>环节类型快照，见 ApproveNodeType。提交时从定义拷贝。</summary>
    public string? NodeType { get; private set; }

    /// <summary>审批方式快照，见 ApproveMode。</summary>
    public string? ApproveMode { get; private set; }

    /// <summary>节点状态，见 ApproveNodeStatus。</summary>
    public string Status { get; private set; } = ApproveNodeStatus.Pending;

    /// <summary>通过规则类型快照，见 ApprovePassRule。</summary>
    public string? PassRuleType { get; private set; }

    /// <summary>通过规则值快照，票决时为百分比。</summary>
    public string? PassRuleValue { get; private set; }

    /// <summary>驳回规则类型快照，见 ApproveRejectRule。</summary>
    public string? RejectRuleType { get; private set; }

    /// <summary>驳回规则值快照。</summary>
    public string? RejectRuleValue { get; private set; }

    /// <summary>进入本节点时间。</summary>
    public DateTime? StartTime { get; private set; }

    /// <summary>离开本节点时间。</summary>
    public DateTime? EndTime { get; private set; }

    /// <summary>创建时间。</summary>
    public DateTime CreateTime { get; private set; } = DateTime.Now;

    private ApproveInstanceNode()
    {
    }

    /// <summary>按流程定义节点生成运行时快照。arrived 为当前环。</summary>
    internal static ApproveInstanceNode CreateFromDef(ApproveProcessNode def, long instanceId, bool arrived)
    {
        var now = DateTime.Now;
        return new ApproveInstanceNode
        {
            Id = IdGenerater.GetNextId(),
            InstanceId = instanceId,
            ProcessNodeId = def.Id,
            NodeKey = def.NodeKey,
            NodeName = def.NodeName,
            NodeType = def.NodeType,
            ApproveMode = def.ApproveMode,
            PassRuleType = def.PassRuleType,
            PassRuleValue = def.PassRuleValue,
            RejectRuleType = def.RejectRuleType,
            RejectRuleValue = def.RejectRuleValue,
            Status = arrived ? ApproveNodeStatus.Running : ApproveNodeStatus.Pending,
            StartTime = arrived ? now : null,
            CreateTime = now,
        };
    }

    /// <summary>单据转到本环。</summary>
    internal void MarkArrived()
    {
        Status = ApproveNodeStatus.Running;
        StartTime = DateTime.Now;
    }

    /// <summary>标记节点已通过。</summary>
    internal void MarkApproved()
    {
        Status = ApproveNodeStatus.Approved;
        EndTime = DateTime.Now;
    }

    /// <summary>标记节点已驳回。</summary>
    internal void MarkRejected()
    {
        Status = ApproveNodeStatus.Rejected;
        EndTime = DateTime.Now;
    }

    /// <summary>标记节点已取消。</summary>
    internal void MarkCancelled()
    {
        Status = ApproveNodeStatus.Cancelled;
        EndTime = DateTime.Now;
    }

    /// <summary>标记节点已跳过。</summary>
    internal void MarkSkipped()
    {
        Status = ApproveNodeStatus.Skipped;
        EndTime = DateTime.Now;
    }
}
