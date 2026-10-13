using XW.Approve.Application.Contracts.Dtos;
using XW.Approve.Domain.Aggregates;
using XW.Approve.Domain.Constants;
using XW.Approve.Domain.Queries;
using XW.Approve.Domain.Rules;

namespace XW.Approve.Application.Mappers;

internal static class ApproveDtoMapper
{
    public static ApproveInstanceDetailDto ToDetail(ApproveInstance instance)
    {
        return new ApproveInstanceDetailDto
        {
            Id = instance.Id,
            ProcessDefId = instance.ProcessDefId,
            BusinessType = instance.BusinessType,
            BusinessId = instance.BusinessId,
            Title = instance.Title,
            SubmitUserId = instance.SubmitUserId,
            SubmitDeptId = instance.SubmitDeptId,
            CurrentNodeId = instance.CurrentNodeId,
            Status = instance.Status,
            FormSnapshot = instance.FormSnapshot,
            StartTime = instance.StartTime,
            EndTime = instance.EndTime,
            Nodes = instance.Nodes
                .OrderBy(x => x.CreateTime)
                .ThenBy(x => x.Id)
                .Select(n => new ApproveInstanceNodeDto
                {
                    Id = n.Id,
                    ProcessNodeId = n.ProcessNodeId,
                    NodeKey = n.NodeKey,
                    NodeName = n.NodeName,
                    NodeType = n.NodeType ?? string.Empty,
                    ApproveMode = n.ApproveMode,
                    Status = n.Status,
                    StartTime = n.StartTime,
                    EndTime = n.EndTime,
                })
                .ToList(),
            Tasks = instance.Tasks
                .OrderBy(x => x.CreateTime)
                .ThenBy(x => x.Id)
                .Select(t => new ApproveTaskDto
                {
                    Id = t.Id,
                    InstanceNodeId = t.InstanceNodeId,
                    AssigneeUserId = t.AssigneeUserId,
                    DeptId = t.DeptId,
                    PositionId = t.PositionId,
                    TaskType = t.TaskType,
                    Status = t.Status,
                    Comment = t.Comment,
                    Payload = t.Payload,
                    CreateTime = t.CreateTime,
                    CompleteTime = t.CompleteTime,
                })
                .ToList(),
            Histories = instance.Histories
                .OrderBy(x => x.OperateTime)
                .ThenBy(x => x.Id)
                .Select(h => new ApproveHistoryDto
                {
                    Id = h.Id,
                    InstanceNodeId = h.InstanceNodeId,
                    TaskId = h.TaskId,
                    OperatorUserId = h.OperatorUserId,
                    Action = h.Action,
                    Comment = h.Comment,
                    OperateTime = h.OperateTime,
                })
                .ToList(),
        };
    }

    public static ApproveInstanceListItemDto ToListItem(ApproveInstance instance) => new()
    {
        Id = instance.Id,
        BusinessType = instance.BusinessType,
        BusinessId = instance.BusinessId,
        Title = instance.Title,
        Status = instance.Status,
        SubmitUserId = instance.SubmitUserId,
        StartTime = instance.StartTime,
        EndTime = instance.EndTime,
    };

    public static ApproveTaskListItemDto ToTaskListItem(ApproveTaskInboxItem item) => new()
    {
        TaskId = item.Task.Id,
        InstanceId = item.Instance.Id,
        InstanceNodeId = item.Task.InstanceNodeId,
        Title = item.Instance.Title,
        BusinessType = item.Instance.BusinessType,
        BusinessId = item.Instance.BusinessId,
        NodeName = item.Node?.NodeName ?? string.Empty,
        NodeType = string.IsNullOrWhiteSpace(item.Node?.NodeType) ? ApproveNodeType.Approve : item.Node!.NodeType!,
        TaskType = item.Task.TaskType,
        Status = item.Task.Status,
        CreateTime = item.Task.CreateTime,
    };

    public static ApproveTimelineDto ToTimeline(
        ApproveInstance instance,
        long currentUserId,
        IReadOnlyDictionary<long, string> userNames,
        IReadOnlyDictionary<long, string> deptNames,
        IReadOnlyDictionary<long, string> positionNames,
        IReadOnlyDictionary<long, string?> schemas,
        IReadOnlyDictionary<long, List<ApproveTaskFileDto>> filesByTask)
    {
        var ordered = instance.Nodes.OrderBy(x => x.CreateTime).ThenBy(x => x.Id).ToList();
        var steps = ordered
            .Select((node, index) =>
            {
                var tasks = instance.Tasks
                    .Where(t => t.InstanceNodeId == node.Id)
                    .OrderBy(t => t.CreateTime)
                    .ThenBy(t => t.Id)
                    .ToList();
                var nodeType = ResolveNodeType(node, tasks, index == 0);
                return new ApproveTimelineStepDto
                {
                    NodeId = node.Id,
                    NodeName = node.NodeName,
                    NodeType = nodeType,
                    ApproveMode = node.ApproveMode,
                    Status = node.Status,
                    IsCurrent = instance.CurrentNodeId == node.Id
                                && instance.Status == ApproveInstanceStatus.Running,
                    StartTime = node.StartTime ?? (nodeType == ApproveNodeType.Start ? instance.StartTime : null),
                    EndTime = node.EndTime,
                    Assignees = nodeType == ApproveNodeType.Start
                        ? [SubmitterAssignee(instance, userNames)]
                        : tasks.Select(t => ToAssignee(t, instance, currentUserId, userNames, deptNames, positionNames, schemas, filesByTask)).ToList(),
                };
            })
            .ToList();

        return new ApproveTimelineDto
        {
            InstanceId = instance.Id,
            Title = instance.Title,
            Status = instance.Status,
            CanCancel = instance.Status == ApproveInstanceStatus.Running && instance.SubmitUserId == currentUserId,
            StartTime = instance.StartTime,
            EndTime = instance.EndTime,
            Steps = steps,
        };
    }

    private static ApproveTimelineAssigneeDto SubmitterAssignee(
        ApproveInstance instance,
        IReadOnlyDictionary<long, string> userNames) =>
        new()
        {
            TaskId = null,
            UserId = instance.SubmitUserId,
            UserName = NameOf(instance.SubmitUserId, userNames),
            Status = ApproveHistoryAction.Submit,
            Comment = instance.Title,
            CompleteTime = instance.StartTime,
            CanApprove = false,
        };

    private static ApproveTimelineAssigneeDto ToAssignee(
        ApproveTask task,
        ApproveInstance instance,
        long currentUserId,
        IReadOnlyDictionary<long, string> userNames,
        IReadOnlyDictionary<long, string> deptNames,
        IReadOnlyDictionary<long, string> positionNames,
        IReadOnlyDictionary<long, string?> schemas,
        IReadOnlyDictionary<long, List<ApproveTaskFileDto>> filesByTask)
    {
        var userId = task.AssigneeUserId ?? 0;
        var canAct = instance.Status == ApproveInstanceStatus.Running
                     && instance.CurrentNodeId == task.InstanceNodeId
                     && task.Status == ApproveTaskStatus.Pending
                     && task.AssigneeUserId == currentUserId;
        schemas.TryGetValue(task.Id, out var schemaJson);
        return new ApproveTimelineAssigneeDto
        {
            TaskId = task.Id,
            UserId = userId,
            UserName = NameOf(userId, userNames),
            DeptName = task.DeptId is long deptId && deptNames.TryGetValue(deptId, out var dept)
                ? dept
                : null,
            PositionName = task.PositionId is long positionId && positionNames.TryGetValue(positionId, out var position)
                ? position
                : null,
            Status = task.Status,
            Comment = task.Comment,
            Payload = task.Payload,
            FormSchema = ToFormSchema(schemaJson),
            Files = filesByTask.TryGetValue(task.Id, out var files) ? files : [],
            CompleteTime = task.CompleteTime,
            CanApprove = canAct && ApproveToken.Equals(task.TaskType, ApproveTaskType.Approve),
            CanComplete = canAct && ApproveToken.Equals(task.TaskType, ApproveTaskType.Complete),
            CanRead = canAct && ApproveToken.Equals(task.TaskType, ApproveTaskType.Read),
        };
    }

    private static ApproveFormSchemaDto? ToFormSchema(string? json)
    {
        var schema = FormSchemaRules.TryRead(json);
        if (FormSchemaRules.IsBlank(schema) || schema is null)
        {
            return null;
        }

        return new ApproveFormSchemaDto
        {
            AllowAttachment = schema.AllowAttachment,
            AttachmentRequired = schema.AttachmentRequired,
            Fields = schema.Fields.Select(field => new ApproveFormFieldDto
            {
                Key = field.Key,
                Name = field.Name,
                Type = field.Type,
                Required = field.Required,
                Options = field.Options,
            }).ToList(),
        };
    }

    private static string ResolveNodeType(ApproveInstanceNode node, IReadOnlyList<ApproveTask> tasks, bool isFirst)
    {
        if (!string.IsNullOrWhiteSpace(node.NodeType))
        {
            return node.NodeType;
        }

        if (!string.IsNullOrWhiteSpace(node.ApproveMode) || tasks.Count > 0)
        {
            return ApproveNodeType.Approve;
        }

        return isFirst ? ApproveNodeType.Start : ApproveNodeType.End;
    }

    private static string NameOf(long userId, IReadOnlyDictionary<long, string> userNames) =>
        userNames.TryGetValue(userId, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : userId.ToString();

}
