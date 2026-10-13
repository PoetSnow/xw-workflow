namespace XW.Approve.Domain.Constants;

/// <summary>审批实例状态。对应 appr_instance.status。</summary>
public static class ApproveInstanceStatus
{
    /// <summary>运行中。</summary>
    public const string Running = "Running";

    /// <summary>已通过。</summary>
    public const string Approved = "Approved";

    /// <summary>已驳回。</summary>
    public const string Rejected = "Rejected";

    /// <summary>已撤销。</summary>
    public const string Cancelled = "Cancelled";

    /// <summary>已终止。</summary>
    public const string Terminated = "Terminated";
}

/// <summary>流程节点类型。对应 appr_process_node.nodetype。</summary>
public static class ApproveNodeType
{
    /// <summary>开始节点，启动后自动通过并向下流转。</summary>
    public const string Start = "Start";

    /// <summary>审批节点，按解析器派人。</summary>
    public const string Approve = "Approve";

    /// <summary>协作节点。按清单交字段和附件，全部完成才往下。</summary>
    public const string Collaborate = "Collaborate";

    /// <summary>知会节点。全部已读才往下。</summary>
    public const string Notify = "Notify";

    /// <summary>任务节点。第一期与协作相同，文案不同。</summary>
    public const string Task = "Task";

    /// <summary>结束节点，无人可派，实例完成。</summary>
    public const string End = "End";

    /// <summary>要派人、要办理的环节。</summary>
    public static bool IsWork(string? nodeType) =>
        ApproveToken.Equals(nodeType, Approve)
        || ApproveToken.Equals(nodeType, Collaborate)
        || ApproveToken.Equals(nodeType, Notify)
        || ApproveToken.Equals(nodeType, Task);

    /// <summary>协作或任务，可以挂交付清单。</summary>
    public static bool HasForm(string? nodeType) =>
        ApproveToken.Equals(nodeType, Collaborate) || ApproveToken.Equals(nodeType, Task);

    /// <summary>全部办完才通过，不走或签和票决。</summary>
    public static bool IsCollective(string? nodeType) =>
        ApproveToken.Equals(nodeType, Collaborate)
        || ApproveToken.Equals(nodeType, Notify)
        || ApproveToken.Equals(nodeType, Task);
}

/// <summary>审批方式。对应 appr_process_node.approvemode。</summary>
public static class ApproveMode
{
    /// <summary>单人审批，一人通过即过。</summary>
    public const string Single = "Single";

    /// <summary>或签，任意一人通过即过。</summary>
    public const string OrSign = "OrSign";

    /// <summary>会签，全部通过才过。</summary>
    public const string AndSign = "AndSign";

    /// <summary>票决，按百分比通过。</summary>
    public const string Vote = "Vote";
}

/// <summary>节点通过规则。对应 appr_process_node.passruletype。</summary>
public static class ApprovePassRule
{
    /// <summary>全部通过。</summary>
    public const string All = "All";

    /// <summary>任意一人通过。</summary>
    public const string Any = "Any";

    /// <summary>按百分比通过。</summary>
    public const string Percent = "Percent";
}

/// <summary>节点驳回规则。对应 appr_process_node.rejectruletype。</summary>
public static class ApproveRejectRule
{
    /// <summary>任意一人驳回即驳回实例。</summary>
    public const string AnyReject = "AnyReject";
}

/// <summary>审批人解析类型。对应 appr_resolver.resolvertype。</summary>
public static class ApproveResolverType
{
    /// <summary>单据部门上的岗位；无人则沿父级往上找。</summary>
    public const string PositionFromDocDept = "PositionFromDocDept";

    /// <summary>固定真实部门 + 岗位，如战略办 + 主任。</summary>
    public const string PositionInFixedDept = "PositionInFixedDept";

    /// <summary>指定用户 Id。</summary>
    public const string User = "User";

    /// <summary>发起人。</summary>
    public const string Submitter = "Submitter";
}

/// <summary>条件组归属。对应 appr_condition_group.ownertype。</summary>
public static class ApproveConditionOwner
{
    /// <summary>管节点出口连线。</summary>
    public const string Transition = "Transition";

    /// <summary>管选哪条流程绑定。</summary>
    public const string Binding = "Binding";
}

/// <summary>流程定义状态。对应 appr_process_def.status。</summary>
public static class ApproveProcessStatus
{
    /// <summary>草稿，不可用于发起。</summary>
    public const string Draft = "Draft";

    /// <summary>已发布，可发起实例。</summary>
    public const string Active = "Active";

    /// <summary>已停用。</summary>
    public const string Disabled = "Disabled";
}

/// <summary>实例节点状态。对应 appr_instance_node.status。</summary>
public static class ApproveNodeStatus
{
    /// <summary>待进入。</summary>
    public const string Pending = "Pending";

    /// <summary>进行中。</summary>
    public const string Running = "Running";

    /// <summary>已通过。</summary>
    public const string Approved = "Approved";

    /// <summary>已驳回。</summary>
    public const string Rejected = "Rejected";

    /// <summary>已跳过。</summary>
    public const string Skipped = "Skipped";

    /// <summary>已取消。</summary>
    public const string Cancelled = "Cancelled";
}

/// <summary>待办任务状态。对应 appr_task.status。</summary>
public static class ApproveTaskStatus
{
    /// <summary>已解析、环节未到。不进待办箱。</summary>
    public const string Scheduled = "Scheduled";

    /// <summary>待处理。</summary>
    public const string Pending = "Pending";

    /// <summary>已通过。</summary>
    public const string Approved = "Approved";

    /// <summary>已驳回。</summary>
    public const string Rejected = "Rejected";

    /// <summary>已取消。</summary>
    public const string Cancelled = "Cancelled";

    /// <summary>已转交。</summary>
    public const string Transferred = "Transferred";

    /// <summary>已跳过。</summary>
    public const string Skipped = "Skipped";

    /// <summary>协作或任务已完成。</summary>
    public const string Completed = "Completed";

    /// <summary>知会已读。</summary>
    public const string Read = "Read";
}

/// <summary>待办任务类型。对应 appr_task.tasktype。</summary>
public static class ApproveTaskType
{
    /// <summary>审批。</summary>
    public const string Approve = "Approve";

    /// <summary>协作或任务，办完即完成。</summary>
    public const string Complete = "Complete";

    /// <summary>知会，已读即完成。</summary>
    public const string Read = "Read";

    /// <summary>按环节类型得到待办类型。</summary>
    public static string ForNode(string? nodeType)
    {
        if (ApproveToken.Equals(nodeType, ApproveNodeType.Notify))
        {
            return Read;
        }

        if (ApproveNodeType.HasForm(nodeType))
        {
            return Complete;
        }

        return Approve;
    }
}

/// <summary>条件组逻辑。对应 appr_condition_group.grouplogic。</summary>
public static class ApproveGroupLogic
{
    /// <summary>全部命中。</summary>
    public const string And = "And";

    /// <summary>任一命中。</summary>
    public const string Or = "Or";
}

/// <summary>条件组类型。对应 appr_condition_group.grouptype。</summary>
public static class ApproveGroupType
{
    /// <summary>根组。</summary>
    public const string Root = "Root";

    /// <summary>嵌套组。</summary>
    public const string Group = "Group";
}

/// <summary>条件项右侧值类型。对应 appr_condition_item.valuetype。</summary>
public static class ApproveValueType
{
    /// <summary>固定值。</summary>
    public const string Const = "Const";

    /// <summary>快照中的另一字段。</summary>
    public const string Field = "Field";

    /// <summary>发起人用户 Id。</summary>
    public const string Initiator = "Initiator";

    /// <summary>发起人部门 Id。</summary>
    public const string InitiatorDept = "InitiatorDept";
}

/// <summary>业务字段值来源。对应 appr_business_field.valuesource。</summary>
public static class ApproveValueSource
{
    /// <summary>表单填写。</summary>
    public const string Form = "Form";

    /// <summary>系统带出。</summary>
    public const string System = "System";

    /// <summary>计算得出。</summary>
    public const string Derived = "Derived";
}

/// <summary>协作清单字段类型。不是条件字段。</summary>
public static class ApproveFormFieldType
{
    /// <summary>单行文本。</summary>
    public const string String = "String";

    /// <summary>多行文本。</summary>
    public const string Text = "Text";

    /// <summary>数字。</summary>
    public const string Number = "Number";

    /// <summary>日期，yyyy-MM-dd。</summary>
    public const string Date = "Date";

    /// <summary>枚举，options 必填。</summary>
    public const string Enum = "Enum";
}

/// <summary>业务字段类型。对应 appr_business_field.fieldtype。</summary>
public static class ApproveFieldType
{
    /// <summary>数字。</summary>
    public const string Number = "Number";

    /// <summary>文本。</summary>
    public const string String = "String";

    /// <summary>枚举。</summary>
    public const string Enum = "Enum";

    /// <summary>用户。</summary>
    public const string User = "User";

    /// <summary>部门。</summary>
    public const string Department = "Department";
}

/// <summary>条件运算符。对应 appr_condition_item.operator。</summary>
public static class ApproveOperator
{
    /// <summary>等于。</summary>
    public const string Equal = "=";

    /// <summary>不等于。</summary>
    public const string NotEqual = "!=";

    /// <summary>大于。</summary>
    public const string GreaterThan = ">";

    /// <summary>大于等于。</summary>
    public const string GreaterThanOrEqual = ">=";

    /// <summary>小于。</summary>
    public const string LessThan = "<";

    /// <summary>小于等于。</summary>
    public const string LessThanOrEqual = "<=";

    /// <summary>包含于列表。</summary>
    public const string In = "In";

    /// <summary>不在列表中。</summary>
    public const string NotIn = "NotIn";

    /// <summary>文本包含。</summary>
    public const string Contains = "Contains";
}

/// <summary>字符串令牌比较：忽略大小写和下划线，兼容旧值 or_sign 与 OrSign。</summary>
public static class ApproveToken
{
    public static bool Equals(string? actual, string expected) =>
        string.Equals(Normalize(actual), Normalize(expected), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string? value) =>
        (value ?? string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
}

/// <summary>FieldKey 即业务表列名，只允许字母数字下划线，防止拼进 SQL。</summary>
public static class ApproveFieldKey
{
    public static bool IsColumnName(string? key) =>
        !string.IsNullOrWhiteSpace(key)
        && key.Length <= 64
        && key[0] is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z')
        && key.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_');
}
