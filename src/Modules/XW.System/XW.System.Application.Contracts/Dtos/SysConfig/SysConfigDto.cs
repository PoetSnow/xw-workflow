namespace XW.System.Application.Contracts.Dtos.SysConfig;

/// <summary>
/// 系统配置详情
/// </summary>
[Serializable]
public class SysConfigDto : SysConfigCreationDto
{
    /// <summary>主键</summary>
    public long Id { get; set; }
}
