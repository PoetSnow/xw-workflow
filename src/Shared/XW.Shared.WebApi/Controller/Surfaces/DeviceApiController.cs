using Microsoft.AspNetCore.Authorization;
using XW.Shared.WebApi.Filters;
using XW.Shared.WebApi.Routing;

namespace Microsoft.AspNetCore.Mvc;

/// <summary>
/// Base controller for device / IoT APIs (<c>api/device/...</c>).
/// 设备域使用 <c>X-Device-Token</c> + <see cref="DeviceTokenMiddleware"/> 鉴权，不走 JWT。
/// </summary>
[ApiController]
[ApiExplorerSettings(GroupName = ApiSurfaces.DeviceGroup)]
[Route(ApiSurfaces.DeviceRouteTemplate)]
[AllowAnonymous]
[ServiceFilter(typeof(DeviceApiEnvelopeResultFilter))]
public abstract class DeviceApiController : XWControllerBase;
