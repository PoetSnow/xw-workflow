namespace XW.Approve.Application.Contracts.Dtos;

/// <summary>发起审批。</summary>
public sealed class ApproveSubmitDto
{
    /// <summary>业务类型，如 Purchase。</summary>
    public string BusinessType { get; set; } = string.Empty;

    /// <summary>业务主键，数字、单号或 UUID。</summary>
    public string BusinessId { get; set; } = string.Empty;

    /// <summary>审批标题。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>可选附加快照。条件字段由引擎按 FieldKey 查业务表列，不读这里。</summary>
    public string FormSnapshot { get; set; } = "{}";

    /// <summary>业务子类型，可空。</summary>
    public string? BusinessSubType { get; set; }
}

/// <summary>通过或驳回。</summary>
public sealed class ApproveHandleDto
{
    /// <summary>处理意见。</summary>
    public string Comment { get; set; } = string.Empty;
}

/// <summary>协作或任务完成。只交字段值，附件另走上传。</summary>
public sealed class ApproveCompleteDto
{
    /// <summary>清单字段。没有清单时为空对象。</summary>
    public System.Text.Json.JsonElement Payload { get; set; }
}

/// <summary>撤销。</summary>
public sealed class ApproveCancelDto
{
    /// <summary>撤销原因。</summary>
    public string Comment { get; set; } = string.Empty;
}

/// <summary>我的待办 / 我发起的筛选。</summary>
public sealed class ApproveSearchDto : SearchPagedDto
{
    /// <summary>实例状态筛选。</summary>
    public string? Status { get; set; }
}

/// <summary>审批实例摘要。</summary>
public sealed class ApproveInstanceListItemDto
{
    public long Id { get; set; }
    public string BusinessType { get; set; } = string.Empty;
    public string BusinessId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long SubmitUserId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}

/// <summary>待办列表项。</summary>
public sealed class ApproveTaskListItemDto
{
    public long TaskId { get; set; }
    public long InstanceId { get; set; }
    public long InstanceNodeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string BusinessType { get; set; } = string.Empty;
    public string BusinessId { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; }
}

/// <summary>实例详情。</summary>
public sealed class ApproveInstanceDetailDto
{
    public long Id { get; set; }
    public long ProcessDefId { get; set; }
    public string BusinessType { get; set; } = string.Empty;
    public string BusinessId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public long SubmitUserId { get; set; }
    public long SubmitDeptId { get; set; }
    public long? CurrentNodeId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string FormSnapshot { get; set; } = "{}";
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<ApproveInstanceNodeDto> Nodes { get; set; } = [];
    public List<ApproveTaskDto> Tasks { get; set; } = [];
    public List<ApproveHistoryDto> Histories { get; set; } = [];
}

/// <summary>实例节点。</summary>
public sealed class ApproveInstanceNodeDto
{
    public long Id { get; set; }
    public long ProcessNodeId { get; set; }
    public string NodeKey { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public string? ApproveMode { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}

/// <summary>任务。</summary>
public sealed class ApproveTaskDto
{
    public long Id { get; set; }
    public long InstanceNodeId { get; set; }
    public long? AssigneeUserId { get; set; }
    public long? DeptId { get; set; }
    public long? PositionId { get; set; }
    public string TaskType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime? CompleteTime { get; set; }
}

/// <summary>历史。</summary>
public sealed class ApproveHistoryDto
{
    public long Id { get; set; }
    public long? InstanceNodeId { get; set; }
    public long? TaskId { get; set; }
    public long OperatorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public DateTime OperateTime { get; set; }
}

/// <summary>一张单据上的全部审批轮次。驳回后再提交会另起一轮，旧轮次保留。</summary>
public sealed class ApproveTimelineBoardDto
{
    public List<ApproveTimelineDto> Rounds { get; set; } = [];
}

/// <summary>一次提交形成的时间线。</summary>
public sealed class ApproveTimelineDto
{
    public int Round { get; set; }
    public long InstanceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
    public bool CanCancel { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<ApproveTimelineStepDto> Steps { get; set; } = [];
}

/// <summary>时间线上一环。</summary>
public sealed class ApproveTimelineStepDto
{
    public long NodeId { get; set; }
    public string NodeName { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public string? ApproveMode { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<ApproveTimelineAssigneeDto> Assignees { get; set; } = [];
}

/// <summary>一环里的一个人。</summary>
public sealed class ApproveTimelineAssigneeDto
{
    public long? TaskId { get; set; }
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? DeptName { get; set; }
    public string? PositionName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public ApproveFormSchemaDto? FormSchema { get; set; }
    public List<ApproveTaskFileDto> Files { get; set; } = [];
    public DateTime? CompleteTime { get; set; }
    public bool CanApprove { get; set; }
    public bool CanComplete { get; set; }
    public bool CanRead { get; set; }
}

/// <summary>待办附件。</summary>
public sealed class ApproveTaskFileDto
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
}

/// <summary>下载附件时的文件流。</summary>
public sealed class ApproveTaskFileContent
{
    public required Stream Stream { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
}
