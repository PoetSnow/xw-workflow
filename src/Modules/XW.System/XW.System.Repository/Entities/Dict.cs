using XW.Infra.Repository;

namespace XW.System.Repository.Entities;

/// <summary>
/// Dictionary
/// </summary>
public class Dict : EfFullAuditEntity
{
    /// <summary>编码最大长度。</summary>
    public static readonly int Code_MaxLength = 32;
    /// <summary>名称最大长度。</summary>
    public static readonly int Name_MaxLength = 32;
    /// <summary>备注最大长度。</summary>
    public static readonly int Remark_MaxLength = 128;

    /// <summary>编码。</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>备注。</summary>
    public string Remark { get; set; } = string.Empty;

    /// <summary>状态。</summary>
    public bool Status { get; set; }
}

/// <summary>
/// Dictionary data
/// </summary>
public class DictData : EfFullAuditEntity
{
    public const int Label_MaxLength = 32;
    public const int Value_MaxLength = 32;
    public const int TagType_MaxLength = 32;

    /// <summary>Dict Code。</summary>
    public string DictCode { get; set; } = string.Empty;

    /// <summary>Label。</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Value。</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Tag类型。</summary>
    public string TagType { get; set; } = string.Empty;

    /// <summary>状态。</summary>
    public bool Status { get; set; }

    /// <summary>Ordinal。</summary>
    public int Ordinal { get; set; }
}
