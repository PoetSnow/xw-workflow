using XW.Shared.WebApi.Routing;

namespace Microsoft.AspNetCore.Mvc;

/// <summary>
/// Base controller for customer portal APIs (<c>api/portal/...</c>).
/// </summary>
[ApiController]
[ApiExplorerSettings(GroupName = ApiSurfaces.PortalGroup)]
[Route(ApiSurfaces.PortalRouteTemplate)]
public abstract class PortalApiController : XWControllerBase;
