namespace XW.System.Application.Contracts.Dtos.Role;

/// <summary>
/// 角色权限分配参数
/// </summary>
public class RoleSetPermissonsDto : IDto
{
    /// <summary>角色 Id</summary>
    public long RoleId { get; set; }

    /// <summary>菜单权限 Id 列表</summary>
    public long[] Permissions { get; set; } = [];
}
