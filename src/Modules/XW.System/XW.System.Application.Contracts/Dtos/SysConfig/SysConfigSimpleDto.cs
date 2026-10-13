namespace XW.System.Application.Contracts.Dtos.SysConfig;

/// <summary>
/// 系统配置简要信息
/// </summary>
[Serializable]
public class SysConfigSimpleDto
{
    /// <summary>配置键</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>配置名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>配置值</summary>
    public string Value { get; set; } = string.Empty;
}
