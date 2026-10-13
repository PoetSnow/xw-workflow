using System.Text.Json.Serialization;

namespace XW.System.Application.Contracts.Dtos.Menu;

/// <summary>
/// 路由树节点
/// </summary>
public sealed class RouterTreeDto
{
    /// <summary>路由名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>路由路径</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>组件路径</summary>
    public string Component { get; set; } = string.Empty;

    /// <summary>重定向路径</summary>
    public string Redirect { get; set; } = string.Empty;

    /// <summary>路由元数据</summary>
    public required RouteMeta Meta { get; set; }

    /// <summary>子路由列表</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RouterTreeDto>? Children { get; set; }

    /// <summary>
    /// 路由元数据
    /// </summary>
    public sealed class RouteMeta
    {
        /// <summary>标题</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>图标</summary>
        public string Icon { get; set; } = string.Empty;

        /// <summary>是否隐藏</summary>
        public bool Hidden { get; set; }

        /// <summary>是否始终显示</summary>
        public bool AlwaysShow { get; set; }

        /// <summary>是否缓存页面</summary>
        public bool KeepAlive { get; set; }
    }
}
