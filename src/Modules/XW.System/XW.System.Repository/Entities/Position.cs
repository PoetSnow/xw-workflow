using XW.Infra.Repository;

namespace XW.System.Repository.Entities;

/// <summary>岗位目录。不绑人、不绑部门。</summary>
public class Position : EfFullAuditEntity
{
    /// <summary>名称最大长度。</summary>
    public static readonly int Name_MaxLength = 32;
    /// <summary>编码最大长度。</summary>
    public static readonly int Code_MaxLength = 32;

    /// <summary>名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>编码。</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>职级，只用于比较高低。</summary>
    public int Level { get; set; }

    /// <summary>启用状态。</summary>
    public bool Status { get; set; }

    /// <summary>排序号。</summary>
    public int? Ordinal { get; set; }
}
