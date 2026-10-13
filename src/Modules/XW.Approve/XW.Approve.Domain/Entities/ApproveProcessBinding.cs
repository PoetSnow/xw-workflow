using XW.Shared.Domain.Entities;

namespace XW.Approve.Domain.Entities;

/// <summary>业务与流程版本绑定。条件命中后选用具体 ProcessDef 版本。</summary>
public class ApproveProcessBinding : FullAuditEntity
{
    /// <summary>业务类型最大长度。</summary>
    public static readonly int BusinessTypeMaxLength = 32;

    /// <summary>业务子类型最大长度。</summary>
    public static readonly int BusinessSubTypeMaxLength = 32;

    /// <summary>业务类型。</summary>
    public string BusinessType { get; set; } = string.Empty;

    /// <summary>业务子类型，可空字符串表示不区分。</summary>
    public string BusinessSubType { get; set; } = string.Empty;

    /// <summary>绑定的流程定义版本 Id。</summary>
    public long ProcessDefId { get; set; }

    /// <summary>优先级，数值越小越先匹配。</summary>
    public int Priority { get; set; }

    /// <summary>本业务其它条件都未命中时走这条。</summary>
    public bool IsDefault { get; set; }

    /// <summary>是否启用。</summary>
    public bool Enabled { get; set; } = true;
}
