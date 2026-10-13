namespace XW.System.Application.Contracts.Dtos.Role;

/// <summary>
/// 角色权限编码
/// </summary>
[Serializable]
public class RoleMenuCodeDto : IDto
{
    /// <summary>角色 Id</summary>
    public long RoleId { get; set; }

    /// <summary>权限编码列表</summary>
    public string[] Perms { get; set; } = [];
}
