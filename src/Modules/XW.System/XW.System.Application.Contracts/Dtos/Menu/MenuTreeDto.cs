using System.Text.Json.Serialization;

namespace XW.System.Application.Contracts.Dtos.Menu;

/// <summary>
/// 菜单树节点
/// </summary>
[Serializable]
public class MenuTreeDto : MenuDto
{
    /// <summary>子菜单列表</summary>
    [JsonPropertyOrder(100)]
    public List<MenuTreeDto> Children { get; set; } = [];
}
