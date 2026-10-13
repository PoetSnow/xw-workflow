using XW.Infra.Repository;

namespace XW.System.Repository.Entities;

/// <summary>
/// Organization
/// </summary>
public class Organization : EfFullAuditEntity
{
    /// <summary>名称最大长度。</summary>
    public static readonly int Name_MaxLength = 32;
    /// <summary>编码最大长度。</summary>
    public static readonly int Code_MaxLength = 16;
    /// <summary>Pids最大长度。</summary>
    public static readonly int Pids_MaxLength = 128;

    /// <summary>Parent Id。</summary>
    public long ParentId { get; set; }

    /// <summary>Parent Ids。</summary>
    public string ParentIds { get; set; } = string.Empty;

    /// <summary>编码。</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>状态。</summary>
    public bool Status { get; set; }

    /// <summary>Ordinal。</summary>
    public int Ordinal { get; set; }
}
