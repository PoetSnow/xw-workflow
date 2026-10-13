namespace XW.System.Application.Contracts.Dtos.Menu;

/// <summary>
/// 菜单创建参数
/// </summary>
public class MenuCreationDto : InputDto
{
    /// <summary>父级 Id</summary>
    public long ParentId { get; set; }

    /// <summary>名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>权限编码</summary>
    public string Perm { get; set; } = string.Empty;

    /// <summary>路由名称</summary>
    public string RouteName { get; set; } = string.Empty;

    /// <summary>路由路径</summary>
    public string RoutePath { get; set; } = string.Empty;

    /// <summary>菜单类型</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>组件路径</summary>
    public string Component { get; set; } = string.Empty;

    /// <summary>是否可见</summary>
    public bool Visible { get; set; }

    /// <summary>重定向路径</summary>
    public string Redirect { get; set; } = string.Empty;

    /// <summary>图标</summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>是否缓存页面</summary>
    public bool KeepAlive { get; set; }

    /// <summary>是否始终显示子路由</summary>
    public bool AlwaysShow { get; set; }

    /// <summary>路由参数</summary>
    public List<KeyValuePair<string, string>> Params { get; set; } = [];

    /// <summary>排序号</summary>
    public int Ordinal { get; set; }
}
