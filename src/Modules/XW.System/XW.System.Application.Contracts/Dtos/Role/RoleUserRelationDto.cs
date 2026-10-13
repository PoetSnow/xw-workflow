namespace XW.System.Application.Contracts.Dtos.Role;

/// <summary>用户-角色关系，供其它模块按角色找人。</summary>
public sealed class RoleUserRelationDto
{
    /// <summary>角色 Id。</summary>
    public long RoleId { get; set; }

    /// <summary>用户 Id。</summary>
    public long UserId { get; set; }
}
