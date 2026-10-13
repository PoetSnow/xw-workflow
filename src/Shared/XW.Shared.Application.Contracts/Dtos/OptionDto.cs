namespace XW.Shared.Application.Contracts.Dtos;

/// <summary>
/// 树形选项
/// </summary>
public sealed class OptionTreeDto
{
    /// <summary>显示标签</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>选项值</summary>
    public long Value { get; set; }

    /// <summary>子节点</summary>
    public List<OptionTreeDto> Children { get; set; } = [];
}
