using XW.Infra.Repository;

namespace XW.Approve.Domain.Entities;

/// <summary>流程连线。XOR：按 Priority 第一条命中，否则走 IsDefault。</summary>
public class ApproveProcessTransition : EfFullAuditEntity
{
    /// <summary>连线名称最大长度。</summary>
    public static readonly int NameMaxLength = 128;

    /// <summary>所属流程定义 Id。</summary>
    public long ProcessDefId { get; set; }

    /// <summary>起点节点 Id。</summary>
    public long FromNodeId { get; set; }

    /// <summary>终点节点 Id。</summary>
    public long ToNodeId { get; set; }

    /// <summary>连线名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>优先级，数值越小越先匹配。</summary>
    public int Priority { get; set; }

    /// <summary>本节点其它条件都未命中时走这条。</summary>
    public bool IsDefault { get; set; }
}
