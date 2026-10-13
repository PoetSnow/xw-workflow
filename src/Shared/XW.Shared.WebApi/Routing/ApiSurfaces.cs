namespace XW.Shared.WebApi.Routing;

/// <summary>
/// API route prefixes and Swagger group names by caller surface (who uses the API).
/// </summary>
public static class ApiSurfaces
{
    public const string ConsoleGroup = "console";
    public const string PortalGroup = "portal";
    public const string DeviceGroup = "device";
    public const string InternalGroup = "internal";

    public const string ConsoleRoutePrefix = "api/console";
    public const string PortalRoutePrefix = "api/portal";
    public const string DeviceRoutePrefix = "api/device";
    public const string InternalRoutePrefix = "api/internal";

    public const string ConsoleRouteTemplate = $"{ConsoleRoutePrefix}/[controller]";
    public const string PortalRouteTemplate = $"{PortalRoutePrefix}/[controller]";
    public const string DeviceRouteTemplate = $"{DeviceRoutePrefix}/[controller]";
    public const string InternalRouteTemplate = $"{InternalRoutePrefix}/[controller]";

    public static IReadOnlyList<ApiSurfaceInfo> GetSwaggerSurfaces(bool includeInternal)
    {
        var surfaces = new List<ApiSurfaceInfo>
        {
            new(ConsoleGroup, "管理后台 API", ConsoleRoutePrefix),
            new(PortalGroup, "客户门户 API", PortalRoutePrefix),
            new(DeviceGroup, "设备 API", DeviceRoutePrefix),
        };

        if (includeInternal)
        {
            surfaces.Add(new(InternalGroup, "内部调试 API", InternalRoutePrefix, DevelopmentOnly: true));
        }

        return surfaces;
    }
}

public sealed record ApiSurfaceInfo(
    string GroupName,
    string Title,
    string RoutePrefix,
    bool DevelopmentOnly = false);
