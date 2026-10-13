using XW.Shared.WebApi.Routing;

namespace Microsoft.AspNetCore.Mvc;

/// <summary>
/// Base controller for management console APIs (<c>api/console/...</c>).
/// </summary>
[ApiController]
[ApiExplorerSettings(GroupName = ApiSurfaces.ConsoleGroup)]
[Route(ApiSurfaces.ConsoleRouteTemplate)]
public abstract class ConsoleApiController : XWControllerBase;
