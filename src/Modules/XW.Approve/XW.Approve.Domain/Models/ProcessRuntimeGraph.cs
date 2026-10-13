using XW.Approve.Domain.Constants;
using XW.Approve.Domain.Entities;
using XW.Approve.Domain.Exceptions;

namespace XW.Approve.Domain.Models;

/// <summary>一次流转用到的已发布流程版本。Application 加载后传入聚合，聚合不读库。</summary>
public sealed class ProcessRuntimeGraph
{
    /// <summary>本版本全部节点。</summary>
    public required IReadOnlyList<ApproveProcessNode> Nodes { get; init; }

    /// <summary>本版本全部连线。</summary>
    public required IReadOnlyList<ApproveProcessTransition> Transitions { get; init; }

    /// <summary>本版本条件组。</summary>
    public required IReadOnlyList<ApproveConditionGroup> ConditionGroups { get; init; }

    /// <summary>本版本条件项。</summary>
    public required IReadOnlyList<ApproveConditionItem> ConditionItems { get; init; }

    /// <summary>本版本审批人解析规则。</summary>
    public required IReadOnlyList<ApproveResolver> Resolvers { get; init; }

    /// <summary>按 Id 取节点，不存在则抛领域异常。</summary>
    public ApproveProcessNode RequireNode(long nodeId) =>
        Nodes.FirstOrDefault(x => x.Id == nodeId)
        ?? throw new ApprovalDomainException($"流程节点 {nodeId} 不存在");

    /// <summary>取唯一开始节点。</summary>
    public ApproveProcessNode RequireStartNode()
    {
        var starts = Nodes
            .Where(n => ApproveToken.Equals(n.NodeType, ApproveNodeType.Start))
            .OrderBy(n => n.Ordinal)
            .ThenBy(n => n.Id)
            .ToList();

        if (starts.Count == 0)
        {
            throw new ApprovalDomainException("流程没有开始节点");
        }

        if (starts.Count > 1)
        {
            throw new ApprovalDomainException("流程有多个开始节点");
        }

        return starts[0];
    }

    /// <summary>是否开始节点。</summary>
    public static bool IsStart(ApproveProcessNode node) =>
        ApproveToken.Equals(node.NodeType, ApproveNodeType.Start);

    /// <summary>是否结束节点。</summary>
    public static bool IsEnd(ApproveProcessNode node) =>
        ApproveToken.Equals(node.NodeType, ApproveNodeType.End);

    /// <summary>是否审批节点。</summary>
    public static bool IsApprove(ApproveProcessNode node) =>
        ApproveToken.Equals(node.NodeType, ApproveNodeType.Approve);

    /// <summary>是否要派人办理的环节。</summary>
    public static bool IsWork(ApproveProcessNode node) =>
        ApproveNodeType.IsWork(node.NodeType);
}
