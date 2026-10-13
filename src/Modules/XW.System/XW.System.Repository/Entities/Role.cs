using XW.Infra.Repository;

namespace XW.System.Repository.Entities;

/// <summary>
/// Role
/// </summary>
public class Role : EfFullAuditEntity
{
    /// <summary>名称最大长度。</summary>
    public static readonly int Name_MaxLength = 32;
    /// <summary>编码最大长度。</summary>
    public static readonly int Code_MaxLength = 32;

    /// <summary>名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>编码。</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Data Scope。</summary>
    public int DataScope { get; set; }

    /// <summary>状态。</summary>
    public bool Status { get; set; }

    /// <summary>Ordinal。</summary>
    public int Ordinal { get; set; }
}
