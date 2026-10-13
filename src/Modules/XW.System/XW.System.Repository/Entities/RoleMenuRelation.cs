using XW.Infra.Repository;

namespace XW.System.Repository.Entities;

/// <summary>
/// Menu-role relation
/// </summary>
public class RoleMenuRelation : EfEntity
{
    /// <summary>Menu Id。</summary>
    public long MenuId { get; set; }

    /// <summary>Role Id。</summary>
    public long RoleId { get; set; }
}
