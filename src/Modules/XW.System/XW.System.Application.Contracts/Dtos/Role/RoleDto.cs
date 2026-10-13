namespace XW.System.Application.Contracts.Dtos.Role;

/// <summary>
/// 角色详情
/// </summary>
[Serializable]
public class RoleDto : RoleCreationDto
{
    /// <summary>主键</summary>
    public long Id { get; set; }
}
