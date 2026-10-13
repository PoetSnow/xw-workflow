using XW.Approve.Domain.Constants;
using XW.Infra.Repository;

namespace XW.Approve.Domain.Entities;

/// <summary>条件项。值类型见 ApproveValueType。</summary>
public class ApproveConditionItem : EfFullAuditEntity
{
    /// <summary>字段键最大长度。</summary>
    public static readonly int FieldKeyMaxLength = 64;

    /// <summary>运算符最大长度。</summary>
    public static readonly int OperatorMaxLength = 16;

    /// <summary>比较值最大长度。</summary>
    public static readonly int ValueMaxLength = 1000;

    /// <summary>值类型最大长度。</summary>
    public static readonly int ValueTypeMaxLength = 16;

    /// <summary>所属条件组 Id。</summary>
    public long GroupId { get; set; }

    /// <summary>快照字段键，如 Amount。</summary>
    public string FieldKey { get; set; } = string.Empty;

    /// <summary>运算符，见 ApproveOperator。</summary>
    public string Operator { get; set; } = string.Empty;

    /// <summary>比较值。Field 类型时为另一字段键。</summary>
    public string? Value { get; set; }

    /// <summary>值类型，见 ApproveValueType。</summary>
    public string ValueType { get; set; } = ApproveValueType.Const;

    /// <summary>同组顺序。</summary>
    public int Ordinal { get; set; }
}
