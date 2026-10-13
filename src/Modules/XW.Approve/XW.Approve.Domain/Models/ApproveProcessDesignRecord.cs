using XW.Approve.Domain.Entities;

namespace XW.Approve.Domain.Models;

/// <summary>设计器一次读写的流程版本，含节点、连线、条件和审批人。</summary>
public sealed class ApproveProcessDesignRecord
{
    /// <summary>流程定义。</summary>
    public required ApproveProcessDef Definition { get; init; }

    /// <summary>节点。</summary>
    public required IReadOnlyList<ApproveProcessNode> Nodes { get; init; }

    /// <summary>连线。</summary>
    public required IReadOnlyList<ApproveProcessTransition> Transitions { get; init; }

    /// <summary>条件组。</summary>
    public required IReadOnlyList<ApproveConditionGroup> Groups { get; init; }

    /// <summary>条件项。</summary>
    public required IReadOnlyList<ApproveConditionItem> Items { get; init; }

    /// <summary>审批人解析规则。</summary>
    public required IReadOnlyList<ApproveResolver> Resolvers { get; init; }

    /// <summary>画布坐标 JSON。列不存在时为空，设计器改为自动排列。</summary>
    public string? LayoutJson { get; init; }
}
