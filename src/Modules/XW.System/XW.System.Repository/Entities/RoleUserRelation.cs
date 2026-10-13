using XW.Infra.Repository;

namespace XW.System.Repository.Entities;

/// <summary>
/// User-role relation
/// </summary>
public class RoleUserRelation : EfEntity
{
    /// <summary>User Id。</summary>
    public long UserId { get; set; }

    /// <summary>Role Id。</summary>
    public long RoleId { get; set; }
}
