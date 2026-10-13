namespace XW.System.Application.Contracts.Dtos.Role;

/// <summary>
/// 角色创建参数
/// </summary>
public class RoleCreationDto : InputDto
{
    /// <summary>名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>编码</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>启用状态</summary>
    public bool Status { get; set; }

    /// <summary>数据范围</summary>
    public int DataScope { get; set; }

    /// <summary>排序号</summary>
    public int Ordinal { get; set; }
}
