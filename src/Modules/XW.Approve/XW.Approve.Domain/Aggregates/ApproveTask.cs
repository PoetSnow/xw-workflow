using XW.Approve.Domain.Constants;
using XW.Approve.Domain.Exceptions;
using XW.Approve.Domain.Models;
using XW.Infra.IdGenerater.Yitter;
using XW.Shared.Domain.Entities;

namespace XW.Approve.Domain.Aggregates;

/// <summary>审批待办。聚合内实体，对应 appr_task。</summary>
public class ApproveTask : DomainEntity
{
    /// <summary>任务类型最大长度。</summary>
    public static readonly int TaskTypeMaxLength = 16;

    /// <summary>状态最大长度。</summary>
    public static readonly int StatusMaxLength = 16;

    /// <summary>意见最大长度。</summary>
    public static readonly int CommentMaxLength = 2000;

    /// <summary>所属实例 Id。</summary>
    public long InstanceId { get; private set; }

    /// <summary>所属实例节点 Id。</summary>
    public long InstanceNodeId { get; private set; }

    /// <summary>审批人用户 Id，对应 sys_user.id。</summary>
    public long? AssigneeUserId { get; private set; }

    /// <summary>解析时的部门 Id，对应 sys_organization.id。</summary>
    public long? DeptId { get; private set; }

    /// <summary>解析时的岗位 Id，对应 sys_position.id。</summary>
    public long? PositionId { get; private set; }

    /// <summary>任务类型，见 ApproveTaskType。</summary>
    public string TaskType { get; private set; } = ApproveTaskType.Approve;

    /// <summary>任务状态，见 ApproveTaskStatus。</summary>
    public string Status { get; private set; } = ApproveTaskStatus.Pending;

    /// <summary>处理意见。</summary>
    public string Comment { get; private set; } = string.Empty;

    /// <summary>本待办提交的字段 JSON。不含文件。</summary>
    public string? Payload { get; private set; }

    /// <summary>父任务 Id，转交或加签预留。</summary>
    public long? ParentTaskId { get; private set; }

    /// <summary>转交来源用户 Id。</summary>
    public long? TransferFromUserId { get; private set; }

    /// <summary>被委托人用户 Id。</summary>
    public long? DelegateUserId { get; private set; }

    /// <summary>创建时间。</summary>
    public DateTime CreateTime { get; private set; } = DateTime.Now;

    /// <summary>完成时间。</summary>
    public DateTime? CompleteTime { get; private set; }

    private ApproveTask()
    {
    }

    /// <summary>按解析结果创建任务。arrived 为当前环时才是待办。</summary>
    internal static ApproveTask Create(ApproveInstanceNode node, ResolvedAssignee assignee, bool arrived, string taskType)
    {
        return new ApproveTask
        {
            Id = IdGenerater.GetNextId(),
            InstanceId = node.InstanceId,
            InstanceNodeId = node.Id,
            AssigneeUserId = assignee.UserId,
            DeptId = assignee.DeptId,
            PositionId = assignee.PositionId,
            TaskType = taskType,
            Status = arrived ? ApproveTaskStatus.Pending : ApproveTaskStatus.Scheduled,
            CreateTime = DateTime.Now,
        };
    }

    /// <summary>环节到达，变成可处理待办。</summary>
    internal void Activate()
    {
        Status = ApproveTaskStatus.Pending;
        CreateTime = DateTime.Now;
    }

    /// <summary>当前用户是否可处理本待办。</summary>
    internal bool CanBeHandledBy(long userId) =>
        Status == ApproveTaskStatus.Pending && AssigneeUserId == userId;

    /// <summary>完成任务。动作决定状态：同意、驳回、完成、已读。</summary>
    internal void Complete(string comment, string action, string? payload = null)
    {
        if (ApproveToken.Equals(action, ApproveHistoryAction.Approve))
        {
            Status = ApproveTaskStatus.Approved;
        }
        else if (ApproveToken.Equals(action, ApproveHistoryAction.Reject))
        {
            Status = ApproveTaskStatus.Rejected;
        }
        else if (ApproveToken.Equals(action, ApproveHistoryAction.Complete))
        {
            Status = ApproveTaskStatus.Completed;
        }
        else if (ApproveToken.Equals(action, ApproveHistoryAction.Read))
        {
            Status = ApproveTaskStatus.Read;
        }
        else
        {
            throw new ApprovalDomainException("不支持的办理动作");
        }

        Comment = comment ?? string.Empty;
        if (payload is not null)
        {
            Payload = payload;
        }

        CompleteTime = DateTime.Now;
    }

    /// <summary>随实例撤销而取消。</summary>
    internal void Cancel(string comment)
    {
        Status = ApproveTaskStatus.Cancelled;
        Comment = comment;
        CompleteTime = DateTime.Now;
    }

    /// <summary>节点已通过时跳过其余待办。</summary>
    internal void Skip(string comment)
    {
        Status = ApproveTaskStatus.Skipped;
        Comment = comment;
        CompleteTime = DateTime.Now;
    }
}
