using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XW.Shared.WebApi.Authentication.Processors;
using XW.Shared.WebApi.Authorization.Handlers;

namespace XW.Shared.WebApi.Registrar;

/// <summary>
/// WebApi dependency registrar
/// </summary>
public abstract partial class AbstractWebApiDependencyRegistrar
{
    public AbstractWebApiDependencyRegistrar(IServiceCollection services, IServiceInfo serviceInfo, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services, $"{nameof(IServiceCollection)} is null.");
        ArgumentNullException.ThrowIfNull(serviceInfo, $"{nameof(IServiceInfo)} is null.");
        ArgumentNullException.ThrowIfNull(configuration, $"{nameof(IConfiguration)} is null.");

        Services = services;
        ServiceInfo = serviceInfo;
        Configuration = configuration;
    }

    public string Name => "webapi";
    internal IConfiguration Configuration { get; init; }
    internal IServiceCollection Services { get; init; }
    internal IServiceInfo ServiceInfo { get; init; }

    public abstract void AddAdncServices();

    /// <summary>
    /// Module API assemblies whose controllers should be exposed by the Host (monolith).
    /// </summary>
    protected virtual IEnumerable<Assembly> GetModuleApiAssemblies() => [];

    /// <summary>
    /// Registers common WebApi services.
    /// </summary>
    public void AddWebApiDefaultServices()
        => AddWebApiDefaultServices<BearerAuthenticationCacheProcessor, PermissionCacheHandler>();

    /// <summary>
    /// Registers common WebApi services.
    /// </summary>
    /// <typeparam name="TAuthenticationProcessor"><see cref="AbstractAuthenticationProcessor"/></typeparam>
    /// <typeparam name="TAuthorizationHandler"><see cref="AbstractPermissionHandler"/></typeparam>
    public void AddWebApiDefaultServices<TAuthenticationProcessor, TAuthorizationHandler>()
        where TAuthenticationProcessor : AbstractAuthenticationProcessor
        where TAuthorizationHandler : AbstractPermissionHandler
    {
        Services
            .Configure<ThreadPoolSettings>(Configuration.GetSection(NodeConsts.ThreadPoolSettings))
            .AddHttpContextAccessor()
            .AddMemoryCache();

        AddControllers();
        AddAuthentication<TAuthenticationProcessor>();
        AddAuthorization<TAuthorizationHandler>();
        AddCors();

        var enableSwaggerUI = Configuration.GetValue(NodeConsts.SwaggerUI_Enable, true);
        if (enableSwaggerUI)
        {
            AddSwaggerGen();
            AddMiniProfiler();
        }
    }
}
