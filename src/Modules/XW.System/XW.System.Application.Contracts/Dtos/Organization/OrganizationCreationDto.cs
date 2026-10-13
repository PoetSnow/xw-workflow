namespace XW.System.Application.Contracts.Dtos.Organization;

/// <summary>
/// 组织创建参数
/// </summary>
public class OrganizationCreationDto : InputDto
{
    /// <summary>父级 Id</summary>
    public long ParentId { get; set; }

    /// <summary>编码</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>启用状态</summary>
    public bool Status { get; set; }

    /// <summary>排序号</summary>
    public int Ordinal { get; set; }
}
