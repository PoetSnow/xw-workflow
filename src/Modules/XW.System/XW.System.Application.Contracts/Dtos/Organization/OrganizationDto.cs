namespace XW.System.Application.Contracts.Dtos.Organization;

/// <summary>
/// 组织详情
/// </summary>
[Serializable]
public class OrganizationDto : OrganizationCreationDto
{
    /// <summary>主键</summary>
    public long Id { get; set; }
}
