using XW.Shared.Domain.Entities;

namespace XW.Approve.Domain.Entities;

/// <summary>流程节点定义。节点类型见 ApproveNodeType。</summary>
public class ApproveProcessNode : FullAuditEntity
{
    /// <summary>节点编码最大长度。</summary>
    public static readonly int NodeKeyMaxLength = 64;

    /// <summary>节点名称最大长度。</summary>
    public static readonly int NodeNameMaxLength = 128;

    /// <summary>节点类型最大长度。</summary>
    public static readonly int NodeTypeMaxLength = 16;

    /// <summary>审批方式最大长度。</summary>
    public static readonly int ApproveModeMaxLength = 16;

    /// <summary>所属流程定义 Id。</summary>
    public long ProcessDefId { get; set; }

    /// <summary>节点编码，同一流程版本内唯一。</summary>
    public string NodeKey { get; set; } = string.Empty;

    /// <summary>节点名称。</summary>
    public string NodeName { get; set; } = string.Empty;

    /// <summary>节点类型，见 ApproveNodeType。</summary>
    public string NodeType { get; set; } = string.Empty;

    /// <summary>审批方式，见 ApproveMode。</summary>
    public string? ApproveMode { get; set; }

    /// <summary>通过规则类型，见 ApprovePassRule。</summary>
    public string? PassRuleType { get; set; }

    /// <summary>通过规则值，票决时为百分比。</summary>
    public string? PassRuleValue { get; set; }

    /// <summary>驳回规则类型，见 ApproveRejectRule。</summary>
    public string? RejectRuleType { get; set; }

    /// <summary>驳回规则值。</summary>
    public string? RejectRuleValue { get; set; }

    /// <summary>本环默认交付清单 JSON。审批和知会为空。</summary>
    public string? FormSchema { get; set; }

    /// <summary>展示顺序，不参与流转。</summary>
    public int Ordinal { get; set; }
}
