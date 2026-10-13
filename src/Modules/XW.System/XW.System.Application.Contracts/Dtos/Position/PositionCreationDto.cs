namespace XW.System.Application.Contracts.Dtos.Position;

/// <summary>岗位创建参数</summary>
public class PositionCreationDto : InputDto
{
    /// <summary>名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>编码</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>职级</summary>
    public int Level { get; set; }

    /// <summary>启用状态</summary>
    public bool Status { get; set; }

    /// <summary>排序号</summary>
    public int? Ordinal { get; set; }
}
