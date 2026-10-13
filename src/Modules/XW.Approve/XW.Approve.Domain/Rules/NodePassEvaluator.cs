using XW.Approve.Domain.Aggregates;
using XW.Approve.Domain.Constants;

namespace XW.Approve.Domain.Rules;

/// <summary>按节点上的会签、或签、票决规则，用该节点任务列表判定是否通过。</summary>
public static class NodePassEvaluator
{
    /// <summary>当前节点是否已满足通过条件。</summary>
    public static bool IsPassed(ApproveInstanceNode node, IEnumerable<ApproveTask> instanceTasks)
    {
        var active = ActiveTasks(node, instanceTasks);
        if (ApproveNodeType.IsCollective(node.NodeType))
        {
            var done = ApproveToken.Equals(node.NodeType, ApproveNodeType.Notify)
                ? ApproveTaskStatus.Read
                : ApproveTaskStatus.Completed;
            return active.Count > 0 && active.All(t => t.Status == done);
        }

        var approved = active.Count(t => t.Status == ApproveTaskStatus.Approved);
        var mode = node.ApproveMode;

        if (ApproveToken.Equals(mode, ApproveMode.Single) || ApproveToken.Equals(mode, ApproveMode.OrSign))
        {
            return approved >= 1;
        }

        if (ApproveToken.Equals(mode, ApproveMode.AndSign))
        {
            return active.Count > 0 && approved == active.Count;
        }

        if (ApproveToken.Equals(mode, ApproveMode.Vote) || ApproveToken.Equals(node.PassRuleType, ApprovePassRule.Percent))
        {
            if (active.Count == 0 || !TryReadPercent(node.PassRuleValue, out var percent))
            {
                return false;
            }

            return approved * 100 >= percent * active.Count;
        }

        if (ApproveToken.Equals(node.PassRuleType, ApprovePassRule.All))
        {
            return active.Count > 0 && approved == active.Count;
        }

        return approved >= 1;
    }

    /// <summary>本节点是否还有待办未处理。</summary>
    public static bool HasPending(ApproveInstanceNode node, IEnumerable<ApproveTask> instanceTasks) =>
        ActiveTasks(node, instanceTasks).Any(t => t.Status == ApproveTaskStatus.Pending);

    /// <summary>本节点尚未处理的审批待办。</summary>
    public static IReadOnlyList<ApproveTask> PendingTasks(ApproveInstanceNode node, IEnumerable<ApproveTask> instanceTasks) =>
        ActiveTasks(node, instanceTasks)
            .Where(t => t.Status == ApproveTaskStatus.Pending)
            .ToList();

    private static List<ApproveTask> ActiveTasks(ApproveInstanceNode node, IEnumerable<ApproveTask> instanceTasks) =>
        instanceTasks
            .Where(t => t.InstanceNodeId == node.Id
                        && MatchesKind(node, t)
                        && t.Status is not (ApproveTaskStatus.Cancelled
                            or ApproveTaskStatus.Skipped
                            or ApproveTaskStatus.Transferred
                            or ApproveTaskStatus.Scheduled))
            .ToList();

    private static bool MatchesKind(ApproveInstanceNode node, ApproveTask task)
    {
        if (ApproveToken.Equals(node.NodeType, ApproveNodeType.Notify))
        {
            return ApproveToken.Equals(task.TaskType, ApproveTaskType.Read);
        }

        if (ApproveNodeType.HasForm(node.NodeType))
        {
            return ApproveToken.Equals(task.TaskType, ApproveTaskType.Complete);
        }

        return ApproveToken.Equals(task.TaskType, ApproveTaskType.Approve);
    }

    private static bool TryReadPercent(string? value, out int percent)
    {
        if (int.TryParse(value, out percent) && percent is >= 0 and <= 100)
        {
            return true;
        }

        percent = 0;
        return false;
    }
}
