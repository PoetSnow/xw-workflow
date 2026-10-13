namespace XW.System.Application.Contracts.Dtos.Role;

/// <summary>
/// 角色菜单关联
/// </summary>
[Serializable]
public class RoleMenuRelationDto : IDto
{
    /// <summary>菜单 Id</summary>
    public long MenuId { get; set; }

    /// <summary>角色 Id</summary>
    public long RoleId { get; set; }
}
