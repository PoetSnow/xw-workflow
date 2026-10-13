using XW.Approve.Domain.Constants;
using XW.Infra.Repository;

namespace XW.Approve.Domain.Entities;

/// <summary>审批条件用的业务字段定义。界面用 FieldName，引擎用 FieldKey 读表单快照。</summary>
public class ApproveBusinessField : EfFullAuditEntity
{
    /// <summary>业务类型最大长度。</summary>
    public static readonly int BusinessTypeMaxLength = 32;

    /// <summary>字段键最大长度。</summary>
    public static readonly int FieldKeyMaxLength = 64;

    /// <summary>字段显示名最大长度。</summary>
    public static readonly int FieldNameMaxLength = 64;

    /// <summary>字段类型最大长度。</summary>
    public static readonly int FieldTypeMaxLength = 16;

    /// <summary>值来源最大长度。</summary>
    public static readonly int ValueSourceMaxLength = 16;

    /// <summary>业务类型，如 Purchase。</summary>
    public string BusinessType { get; set; } = string.Empty;

    /// <summary>快照 JSON 的键，如 Amount。</summary>
    public string FieldKey { get; set; } = string.Empty;

    /// <summary>界面展示名。</summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>字段类型，见 ApproveFieldType。</summary>
    public string FieldType { get; set; } = string.Empty;

    /// <summary>值来源，见 ApproveValueSource。</summary>
    public string ValueSource { get; set; } = ApproveValueSource.Form;

    /// <summary>字段配置 JSON，例如枚举选项。</summary>
    public string? FieldConfig { get; set; }

    /// <summary>是否必填。</summary>
    public bool Required { get; set; }

    /// <summary>是否启用。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>展示顺序。</summary>
    public int Ordinal { get; set; }
}
