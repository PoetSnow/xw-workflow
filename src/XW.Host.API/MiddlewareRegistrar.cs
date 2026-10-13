using XW.Shared.WebApi.Middleware;
using XW.Shared.WebApi.Registrar;

namespace XW.Host.API;

public sealed class MiddlewareRegistrar(WebApplication app) : AbstractWebApiMiddlewareRegistrar(app)
{
    public override void UseXW() => UseWebApiDefault(beforeAuthentication: builder => builder.UseInternalApiKeyAuth());
}
