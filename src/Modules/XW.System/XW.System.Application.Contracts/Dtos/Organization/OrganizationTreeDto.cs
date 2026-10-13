namespace XW.System.Application.Contracts.Dtos.Organization;

/// <summary>
/// 组织树节点
/// </summary>
[Serializable]
public class OrganizationTreeDto : OrganizationDto
{
    /// <summary>子组织列表</summary>
    public List<OrganizationTreeDto> Children { get; set; } = [];
}
