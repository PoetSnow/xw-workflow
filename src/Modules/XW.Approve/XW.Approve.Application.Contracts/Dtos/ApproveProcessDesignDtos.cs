namespace XW.Approve.Application.Contracts.Dtos;

/// <summary>流程版本列表项。</summary>
public sealed class ApproveProcessListItemDto
{
    /// <summary>流程定义 Id。</summary>
    public long Id { get; set; }

    /// <summary>流程编码。</summary>
    public string ProcessKey { get; set; } = string.Empty;

    /// <summary>流程名称。</summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>业务类型。</summary>
    public string BusinessType { get; set; } = string.Empty;

    /// <summary>版本号。</summary>
    public int Version { get; set; }

    /// <summary>Draft / Active / Disabled。</summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>新建流程。画布初始为开始节点连到结束节点。</summary>
public sealed class ApproveProcessCreateDto
{
    /// <summary>流程编码，同编码多版本。</summary>
    public string ProcessKey { get; set; } = string.Empty;

    /// <summary>流程名称。</summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>业务类型，如 Purchase。</summary>
    public string BusinessType { get; set; } = string.Empty;
}

/// <summary>设计器一次保存的整张图。</summary>
public class ApproveProcessSaveDto
{
    /// <summary>流程名称。</summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>业务类型。</summary>
    public string BusinessType { get; set; } = string.Empty;

    /// <summary>节点。</summary>
    public List<ApproveDesignNodeDto> Nodes { get; set; } = [];

    /// <summary>连线。</summary>
    public List<ApproveDesignTransitionDto> Transitions { get; set; } = [];
}

/// <summary>设计器读出的整张图。</summary>
public sealed class ApproveProcessDesignDto : ApproveProcessSaveDto
{
    /// <summary>流程定义 Id。</summary>
    public long Id { get; set; }

    /// <summary>流程编码，创建后不可改。</summary>
    public string ProcessKey { get; set; } = string.Empty;

    /// <summary>版本号。</summary>
    public int Version { get; set; }

    /// <summary>Draft / Active / Disabled。</summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>设计器节点。</summary>
public sealed class ApproveDesignNodeDto
{
    /// <summary>节点编码，同一版本内唯一。</summary>
    public string NodeKey { get; set; } = string.Empty;

    /// <summary>节点名称。</summary>
    public string NodeName { get; set; } = string.Empty;

    /// <summary>Start / Approve / Collaborate / Notify / Task / End。</summary>
    public string NodeType { get; set; } = string.Empty;

    /// <summary>Single / OrSign / AndSign / Vote。</summary>
    public string? ApproveMode { get; set; }

    /// <summary>All / Any / Percent。</summary>
    public string? PassRuleType { get; set; }

    /// <summary>票决百分比。</summary>
    public string? PassRuleValue { get; set; }

    /// <summary>驳回规则，审批节点为 AnyReject。</summary>
    public string? RejectRuleType { get; set; }

    /// <summary>协作或任务的默认交付清单。审批和知会为空。</summary>
    public ApproveFormSchemaDto? FormSchema { get; set; }

    /// <summary>画布横坐标。</summary>
    public double X { get; set; }

    /// <summary>画布纵坐标。</summary>
    public double Y { get; set; }

    /// <summary>审批人。开始和结束节点为空。</summary>
    public List<ApproveDesignResolverDto> Resolvers { get; set; } = [];
}

/// <summary>节点上的一条找人规则。</summary>
public sealed class ApproveDesignResolverDto
{
    /// <summary>PositionFromDocDept / PositionInFixedDept / User / Submitter。</summary>
    public string ResolverType { get; set; } = string.Empty;

    /// <summary>岗位编码或用户 Id。Submitter 可空。</summary>
    public string ResolverValue { get; set; } = string.Empty;

    /// <summary>PositionInFixedDept 的固定部门 Id。</summary>
    public long? FixedDeptId { get; set; }

    /// <summary>覆盖节点清单。空则用节点默认清单。</summary>
    public ApproveFormSchemaDto? FormSchema { get; set; }
}

/// <summary>设计器连线。多条出线时按 Priority 做 XOR，未命中走 IsDefault。</summary>
public sealed class ApproveDesignTransitionDto
{
    /// <summary>起点节点编码。</summary>
    public string FromKey { get; set; } = string.Empty;

    /// <summary>终点节点编码。</summary>
    public string ToKey { get; set; } = string.Empty;

    /// <summary>连线名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>优先级，数值越小越先匹配。</summary>
    public int Priority { get; set; }

    /// <summary>其它条件都未命中时走这条。</summary>
    public bool IsDefault { get; set; }

    /// <summary>条件之间的 And / Or。</summary>
    public string GroupLogic { get; set; } = "And";

    /// <summary>条件。默认线不使用条件。</summary>
    public List<ApproveDesignConditionDto> Conditions { get; set; } = [];
}

/// <summary>连线上的一条条件。</summary>
public sealed class ApproveDesignConditionDto
{
    /// <summary>快照字段键，如 Amount。</summary>
    public string FieldKey { get; set; } = string.Empty;

    /// <summary>运算符。</summary>
    public string Operator { get; set; } = "=";

    /// <summary>比较值。值类型为 Field 时是另一个字段键。</summary>
    public string? Value { get; set; }

    /// <summary>Const / Field / Initiator / InitiatorDept。</summary>
    public string ValueType { get; set; } = "Const";
}

/// <summary>条件字段。</summary>
public sealed class ApproveBusinessFieldDto
{
    /// <summary>快照 JSON 的键。</summary>
    public string FieldKey { get; set; } = string.Empty;

    /// <summary>界面名称。</summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>Number / String / Enum / User / Department。</summary>
    public string FieldType { get; set; } = "String";

    /// <summary>Form / System / Derived。</summary>
    public string ValueSource { get; set; } = "Form";

    /// <summary>是否必填。</summary>
    public bool Required { get; set; }

    /// <summary>是否启用。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>展示顺序。</summary>
    public int Ordinal { get; set; }
}

/// <summary>协作或任务的交付清单。</summary>
public sealed class ApproveFormSchemaDto
{
    /// <summary>字段。</summary>
    public List<ApproveFormFieldDto> Fields { get; set; } = [];

    /// <summary>是否允许附件。</summary>
    public bool AllowAttachment { get; set; }

    /// <summary>是否必须上传附件。</summary>
    public bool AttachmentRequired { get; set; }
}

/// <summary>清单字段。</summary>
public sealed class ApproveFormFieldDto
{
    /// <summary>字段键。</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>String / Text / Number / Date / Enum。</summary>
    public string Type { get; set; } = "String";

    /// <summary>是否必填。</summary>
    public bool Required { get; set; }

    /// <summary>枚举选项。</summary>
    public List<string> Options { get; set; } = [];
}
