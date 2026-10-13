using XW.Approve.Domain.Constants;
using XW.Approve.Domain.Entities;
using XW.Approve.Domain.Exceptions;
using XW.Approve.Domain.Models;
using XW.Approve.Domain.Rules;
using XW.Infra.IdGenerater.Yitter;
using XW.Shared.Domain.Entities;

namespace XW.Approve.Domain.Aggregates;

/// <summary>
/// 审批实例聚合根。节点、任务、历史都在本聚合内流转，不单独作为根。
/// </summary>
public class ApproveInstance : AggregateRootWithFullAuditInfo
{
    /// <summary>业务类型最大长度。</summary>
    public static readonly int BusinessTypeMaxLength = 32;

    /// <summary>业务主键最大长度。</summary>
    public static readonly int BusinessIdMaxLength = 64;

    /// <summary>标题最大长度。</summary>
    public static readonly int TitleMaxLength = 256;

    /// <summary>状态最大长度。</summary>
    public static readonly int StatusMaxLength = 16;

    private const int MaxAdvanceHops = 32;

    /// <summary>冻结的流程定义版本 Id。</summary>
    public long ProcessDefId { get; private set; }

    /// <summary>业务类型，如 Purchase。</summary>
    public string BusinessType { get; private set; } = string.Empty;

    /// <summary>业务主键，兼容数字、单号、UUID。</summary>
    public string BusinessId { get; private set; } = string.Empty;

    /// <summary>审批标题。</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>发起人，对应 sys_user.id。</summary>
    public long SubmitUserId { get; private set; }

    /// <summary>提交时发起人部门，对应 sys_organization.id。</summary>
    public long SubmitDeptId { get; private set; }

    /// <summary>当前实例节点 Id。</summary>
    public long? CurrentNodeId { get; private set; }

    /// <summary>实例状态，见 ApproveInstanceStatus。</summary>
    public string Status { get; private set; } = ApproveInstanceStatus.Running;

    /// <summary>提交时表单快照，后续分支只读这份。</summary>
    public string FormSnapshot { get; private set; } = "{}";

    /// <summary>启动时间。</summary>
    public DateTime StartTime { get; private set; } = DateTime.Now;

    /// <summary>结束时间。</summary>
    public DateTime? EndTime { get; private set; }

    /// <summary>运行时节点快照。</summary>
    public ICollection<ApproveInstanceNode> Nodes { get; } = [];

    /// <summary>待办任务。</summary>
    public ICollection<ApproveTask> Tasks { get; } = [];

    /// <summary>操作历史。</summary>
    public ICollection<ApproveHistory> Histories { get; } = [];

    private ApproveInstance()
    {
    }

    /// <summary>
    /// 启动审批实例。开始节点自动通过并往下走；结束节点无人可派即完成。
    /// </summary>
    public static ApproveInstance Start(
        long processDefId,
        string businessType,
        string businessId,
        string title,
        long submitUserId,
        long submitDeptId,
        string formSnapshot,
        ProcessRuntimeGraph graph,
        AssigneeResolveContext org)
    {
        if (processDefId <= 0)
        {
            throw new ApprovalDomainException("流程定义 Id 无效");
        }

        if (string.IsNullOrWhiteSpace(businessType))
        {
            throw new ApprovalDomainException("业务类型不能为空");
        }

        if (string.IsNullOrWhiteSpace(businessId))
        {
            throw new ApprovalDomainException("业务主键不能为空");
        }

        if (submitUserId <= 0)
        {
            throw new ApprovalDomainException("发起人无效");
        }

        if (submitDeptId <= 0)
        {
            throw new ApprovalDomainException("发起人部门无效");
        }

        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(org);

        var instance = new ApproveInstance
        {
            Id = IdGenerater.GetNextId(),
            ProcessDefId = processDefId,
            BusinessType = businessType.Trim(),
            BusinessId = businessId.Trim(),
            Title = title ?? string.Empty,
            SubmitUserId = submitUserId,
            SubmitDeptId = submitDeptId,
            Status = ApproveInstanceStatus.Running,
            FormSnapshot = string.IsNullOrWhiteSpace(formSnapshot) ? "{}" : formSnapshot,
            StartTime = DateTime.Now,
        };

        var path = WalkPath(graph, instance.FormSnapshot, instance.SubmitUserId, instance.SubmitDeptId);
        var firstWorkPending = true;
        ApproveInstanceNode? startNode = null;
        foreach (var def in path)
        {
            var arrived = ProcessRuntimeGraph.IsWork(def) && firstWorkPending;
            var node = instance.MaterializeNode(def, graph, org, arrived);
            if (ProcessRuntimeGraph.IsStart(def))
            {
                startNode = node;
            }

            if (ProcessRuntimeGraph.IsWork(def))
            {
                firstWorkPending = false;
            }
        }

        instance.AppendHistory(
            submitUserId,
            ApproveHistoryAction.Submit,
            comment: title,
            instanceNodeId: startNode?.Id);

        if (firstWorkPending)
        {
            var end = instance.PathOrder().LastOrDefault();
            if (end is not null)
            {
                instance.Arrive(end);
            }
        }

        return instance;
    }

    /// <summary>
    /// 通过。任务完成 → 节点判定 → 选线 → 下一节点 → 新任务。会签未满票则只完成任务。
    /// </summary>
    public void Approve(
        long taskId,
        long userId,
        string comment,
        ProcessRuntimeGraph graph,
        AssigneeResolveContext org)
    {
        EnsureRunning();
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(org);

        var task = RequirePendingTask(taskId, userId);
        EnsureTaskType(task, ApproveTaskType.Approve, "当前环节不是审批");
        task.Complete(comment, ApproveHistoryAction.Approve);

        var node = RequireNode(task.InstanceNodeId);
        AppendHistory(userId, ApproveHistoryAction.Approve, comment, node.Id, task.Id);
        AdvanceIfPassed(node, userId);
    }

    /// <summary>驳回。一票否决，直接结束实例。</summary>
    public void Reject(long taskId, long userId, string comment)
    {
        EnsureRunning();

        var task = RequirePendingTask(taskId, userId);
        EnsureTaskType(task, ApproveTaskType.Approve, "当前环节不能驳回");
        task.Complete(comment, ApproveHistoryAction.Reject);

        var node = RequireNode(task.InstanceNodeId);
        CancelPendingOf(node, "节点已驳回，其余待办取消");
        node.MarkRejected();
        CancelNotReached("节点已驳回，后续环节取消");
        AppendHistory(userId, ApproveHistoryAction.Reject, comment, node.Id, task.Id);
        Finish(ApproveInstanceStatus.Rejected);
    }

    /// <summary>协作或任务完成。全员完成后才往下。</summary>
    public void Complete(long taskId, long userId, string? payloadJson, string? schemaJson, int attachmentCount)
    {
        EnsureRunning();

        var task = RequirePendingTask(taskId, userId);
        EnsureTaskType(task, ApproveTaskType.Complete, "当前环节不是协作或任务");
        var payload = FormSchemaRules.NormalizePayload(schemaJson, payloadJson, attachmentCount);
        task.Complete(string.Empty, ApproveHistoryAction.Complete, payload);

        var node = RequireNode(task.InstanceNodeId);
        AppendHistory(userId, ApproveHistoryAction.Complete, comment: null, node.Id, task.Id, snapshot: payload);
        AdvanceIfPassed(node, userId);
    }

    /// <summary>知会已读。全员已读后才往下。</summary>
    public void MarkRead(long taskId, long userId)
    {
        EnsureRunning();

        var task = RequirePendingTask(taskId, userId);
        EnsureTaskType(task, ApproveTaskType.Read, "当前环节不是知会");
        task.Complete(string.Empty, ApproveHistoryAction.Read);

        var node = RequireNode(task.InstanceNodeId);
        AppendHistory(userId, ApproveHistoryAction.Read, comment: null, node.Id, task.Id);
        AdvanceIfPassed(node, userId);
    }

    /// <summary>撤销。仅发起人可调用。</summary>
    public void Cancel(long userId, string comment)
    {
        EnsureRunning();
        if (userId != SubmitUserId)
        {
            throw new ApprovalDomainException("仅发起人可撤销");
        }

        foreach (var task in Tasks.Where(t =>
                     t.Status is ApproveTaskStatus.Pending or ApproveTaskStatus.Scheduled).ToList())
        {
            task.Cancel(comment ?? "实例撤销");
        }

        foreach (var node in Nodes.Where(n =>
                     n.Status is ApproveNodeStatus.Running or ApproveNodeStatus.Pending).ToList())
        {
            node.MarkCancelled();
        }

        AppendHistory(userId, ApproveHistoryAction.Cancel, comment, CurrentNodeId);
        Finish(ApproveInstanceStatus.Cancelled);
    }

    /// <summary>用当前快照重算尚未到达的后续环节。已提交实例补全漏掉的分支。</summary>
    public bool SyncSnapshotAndPendingPath(
        string snapshot,
        ProcessRuntimeGraph graph,
        AssigneeResolveContext org)
    {
        EnsureRunning();
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(org);

        var changed = false;
        if (!string.IsNullOrWhiteSpace(snapshot)
            && !string.Equals(snapshot, FormSnapshot, StringComparison.Ordinal))
        {
            FormSnapshot = snapshot;
            changed = true;
        }

        return RebuildPendingTail(graph, org) || changed;
    }

    /// <summary>提交时按快照走出唯一路径。</summary>
    private static IReadOnlyList<ApproveProcessNode> WalkPath(
        ProcessRuntimeGraph graph,
        string formSnapshot,
        long submitUserId,
        long submitDeptId)
    {
        var start = graph.RequireStartNode();
        var path = new List<ApproveProcessNode> { start };
        path.AddRange(WalkRemaining(start, graph, formSnapshot, submitUserId, submitDeptId));
        return path;
    }

    private static List<ApproveProcessNode> WalkRemaining(
        ApproveProcessNode current,
        ProcessRuntimeGraph graph,
        string formSnapshot,
        long submitUserId,
        long submitDeptId)
    {
        var remaining = new List<ApproveProcessNode>();
        for (var hop = 0; hop < MaxAdvanceHops; hop++)
        {
            var edge = TransitionSelector.Select(
                current.Id,
                current.NodeKey,
                graph,
                formSnapshot,
                submitUserId,
                submitDeptId);
            if (edge is null)
            {
                break;
            }

            current = graph.RequireNode(edge.ToNodeId);
            remaining.Add(current);
            if (ProcessRuntimeGraph.IsEnd(current))
            {
                return remaining;
            }
        }

        if (remaining.Any(ProcessRuntimeGraph.IsEnd))
        {
            return remaining;
        }

        throw new ApprovalDomainException("流程没有结束节点，或推进超过最大跳转次数");
    }

    private bool RebuildPendingTail(ProcessRuntimeGraph graph, AssigneeResolveContext org)
    {
        var current = CurrentNodeId is long id
            ? RequireNode(id)
            : PathOrder().FirstOrDefault(n => n.Status == ApproveNodeStatus.Running);
        if (current is null)
        {
            return false;
        }

        var expected = WalkRemaining(
            graph.RequireNode(current.ProcessNodeId),
            graph,
            FormSnapshot,
            SubmitUserId,
            SubmitDeptId);
        var stored = PathOrder().SkipWhile(n => n.Id != current.Id).Skip(1).ToList();
        if (expected.Select(x => x.Id).SequenceEqual(stored.Select(x => x.ProcessNodeId)))
        {
            return false;
        }

        foreach (var node in stored)
        {
            foreach (var task in Tasks.Where(t => t.InstanceNodeId == node.Id).ToList())
            {
                Tasks.Remove(task);
            }

            Nodes.Remove(node);
        }

        foreach (var def in expected)
        {
            MaterializeNode(def, graph, org, arrived: false);
        }

        return true;
    }

    /// <summary>把路径上的节点和人落进实例。arrived 的审批环立即进待办。</summary>
    private ApproveInstanceNode MaterializeNode(
        ApproveProcessNode def,
        ProcessRuntimeGraph graph,
        AssigneeResolveContext org,
        bool arrived)
    {
        var node = ApproveInstanceNode.CreateFromDef(def, Id, arrived);
        Nodes.Add(node);

        if (ProcessRuntimeGraph.IsStart(def))
        {
            node.MarkApproved();
            return node;
        }

        if (ProcessRuntimeGraph.IsEnd(def))
        {
            return node;
        }

        var assignees = AssigneeResolver.Resolve(def, graph, org, FormSnapshot, SubmitUserId, SubmitDeptId);
        if (assignees.Count == 0)
        {
            var who = ProcessRuntimeGraph.IsApprove(def) ? "审批人" : "办理人";
            throw new ApprovalDomainException($"节点 {def.NodeKey} 未解析到{who}");
        }

        var taskType = ApproveTaskType.ForNode(def.NodeType);
        foreach (var assignee in assignees)
        {
            Tasks.Add(ApproveTask.Create(node, assignee, arrived, taskType));
        }

        if (arrived)
        {
            CurrentNodeId = node.Id;
        }

        return node;
    }

    /// <summary>上一环通过后，激活已落库的下一环，不再重新找人。</summary>
    private void AdvanceAlongPath(ApproveInstanceNode from)
    {
        var next = NextOnPath(from);
        if (next is null)
        {
            Finish(ApproveInstanceStatus.Approved);
            return;
        }

        Arrive(next);
    }

    private void Arrive(ApproveInstanceNode node)
    {
        CurrentNodeId = node.Id;
        var scheduled = Tasks
            .Where(t => t.InstanceNodeId == node.Id && t.Status == ApproveTaskStatus.Scheduled)
            .ToList();

        if (scheduled.Count == 0)
        {
            if (ApproveNodeType.IsWork(node.NodeType))
            {
                throw new ApprovalDomainException($"节点 {node.NodeKey} 没有已解析的办理人");
            }

            if (string.IsNullOrEmpty(node.ApproveMode))
            {
                node.MarkApproved();
                Finish(ApproveInstanceStatus.Approved);
                return;
            }

            throw new ApprovalDomainException($"节点 {node.NodeKey} 没有已解析的审批人");
        }

        node.MarkArrived();
        foreach (var task in scheduled)
        {
            task.Activate();
        }
    }

    private ApproveInstanceNode? NextOnPath(ApproveInstanceNode from)
    {
        var ordered = PathOrder();
        var index = ordered.FindIndex(n => n.Id == from.Id);
        return index >= 0 && index + 1 < ordered.Count ? ordered[index + 1] : null;
    }

    private List<ApproveInstanceNode> PathOrder() =>
        Nodes.OrderBy(n => n.CreateTime).ThenBy(n => n.Id).ToList();

    private void CancelNotReached(string comment)
    {
        foreach (var task in Tasks.Where(t => t.Status == ApproveTaskStatus.Scheduled).ToList())
        {
            task.Cancel(comment);
        }

        foreach (var node in Nodes.Where(n => n.Status == ApproveNodeStatus.Pending).ToList())
        {
            node.MarkCancelled();
        }
    }

    private ApproveTask RequirePendingTask(long taskId, long userId)
    {
        var task = Tasks.FirstOrDefault(t => t.Id == taskId)
            ?? throw new ApprovalDomainException("任务不存在");

        if (!task.CanBeHandledBy(userId))
        {
            throw new ApprovalDomainException(
                task.Status == ApproveTaskStatus.Pending ? "无权处理该任务" : "任务已处理");
        }

        if (CurrentNodeId != task.InstanceNodeId)
        {
            throw new ApprovalDomainException("任务不属于当前节点");
        }

        return task;
    }

    private ApproveInstanceNode RequireNode(long nodeId) =>
        Nodes.FirstOrDefault(n => n.Id == nodeId)
        ?? throw new ApprovalDomainException("实例节点不存在");

    private void SkipPendingOf(ApproveInstanceNode node, long operatorUserId, string comment)
    {
        foreach (var pending in NodePassEvaluator.PendingTasks(node, Tasks))
        {
            pending.Skip(comment);
            AppendHistory(operatorUserId, ApproveHistoryAction.Skip, comment, node.Id, pending.Id);
        }
    }

    private void CancelPendingOf(ApproveInstanceNode node, string comment)
    {
        foreach (var pending in NodePassEvaluator.PendingTasks(node, Tasks))
        {
            pending.Cancel(comment);
        }
    }

    private void RejectNodeAndInstance(ApproveInstanceNode node, long userId, string comment)
    {
        node.MarkRejected();
        AppendHistory(userId, ApproveHistoryAction.Reject, comment, node.Id);
        Finish(ApproveInstanceStatus.Rejected);
    }

    private void AdvanceIfPassed(ApproveInstanceNode node, long userId)
    {
        if (!NodePassEvaluator.IsPassed(node, Tasks))
        {
            if (NodePassEvaluator.HasPending(node, Tasks))
            {
                return;
            }

            RejectNodeAndInstance(node, userId, "未达到节点通过条件");
            return;
        }

        SkipPendingOf(node, userId, "节点已通过，其余待办跳过");
        node.MarkApproved();
        AdvanceAlongPath(node);
    }

    private static void EnsureTaskType(ApproveTask task, string expected, string message)
    {
        if (!ApproveToken.Equals(task.TaskType, expected))
        {
            throw new ApprovalDomainException(message);
        }
    }

    private void AppendHistory(
        long operatorUserId,
        string action,
        string? comment,
        long? instanceNodeId = null,
        long? taskId = null,
        string? snapshot = null)
    {
        Histories.Add(ApproveHistory.Append(
            Id,
            operatorUserId,
            action,
            comment,
            instanceNodeId,
            taskId,
            snapshot: snapshot ?? FormSnapshot));
    }

    private void EnsureRunning()
    {
        if (Status != ApproveInstanceStatus.Running)
        {
            throw new ApprovalDomainException("审批已结束");
        }
    }

    private void Finish(string status)
    {
        Status = status;
        EndTime = DateTime.Now;
    }
}
