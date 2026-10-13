namespace XW.System.Application.Contracts.Dtos.SysConfig;

/// <summary>
/// 系统配置创建参数
/// </summary>
public class SysConfigCreationDto : InputDto
{
    /// <summary>配置键</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>配置名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>配置值</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>备注</summary>
    public string Remark { get; set; } = string.Empty;
}
