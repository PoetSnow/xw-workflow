using XW.Approve.Domain.Aggregates;
using XW.Approve.Domain.Constants;
using XW.Approve.Domain.Entities;
using XW.Approve.Domain.Exceptions;
using XW.Approve.Domain.Models;

namespace XW.Approve.Domain.Rules;

/// <summary>XOR 出线：按 Priority 第一条命中，否则走 IsDefault。</summary>
public static class TransitionSelector
{
    /// <summary>选出当前节点的下一条连线。没有出线时返回 null，表示流程结束。</summary>
    public static ApproveProcessTransition? Select(
        ApproveInstanceNode fromNode,
        ProcessRuntimeGraph graph,
        string formSnapshot,
        long submitUserId,
        long submitDeptId)
    {
        return Select(
            fromNode.ProcessNodeId,
            fromNode.NodeKey,
            graph,
            formSnapshot,
            submitUserId,
            submitDeptId);
    }

    /// <summary>按流程节点 Id 选出线。提交时走完整路径用。</summary>
    public static ApproveProcessTransition? Select(
        long fromProcessNodeId,
        string fromNodeKey,
        ProcessRuntimeGraph graph,
        string formSnapshot,
        long submitUserId,
        long submitDeptId)
    {
        var outs = graph.Transitions
            .Where(t => t.FromNodeId == fromProcessNodeId)
            .OrderBy(t => t.Priority)
            .ThenBy(t => t.Id)
            .ToList();

        if (outs.Count == 0)
        {
            return null;
        }

        foreach (var edge in outs.Where(t => !t.IsDefault))
        {
            if (!ConditionEvaluator.HasRootGroups(edge.Id, ApproveConditionOwner.Transition, graph.ConditionGroups))
            {
                continue;
            }

            if (ConditionEvaluator.Matches(
                    edge.Id,
                    ApproveConditionOwner.Transition,
                    graph.ConditionGroups,
                    graph.ConditionItems,
                    formSnapshot,
                    submitUserId,
                    submitDeptId))
            {
                return edge;
            }
        }

        var fallback = outs.FirstOrDefault(t => t.IsDefault);
        if (fallback is not null)
        {
            return fallback;
        }

        throw new ApprovalDomainException($"节点 {fromNodeKey} 没有命中的出线，也没有默认线");
    }
}
