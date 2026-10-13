using System.Reflection;
using XW.Host.API.Infrastructure;
using XW.Host.API.Modules;
using XW.Shared;
using XW.Shared.WebApi.Middleware;
using XW.Shared.WebApi.Registrar;
namespace XW.Host.API;

/// <summary>
/// 
/// </summary>
/// <param name="services"></param>
/// <param name="serviceInfo"></param>
/// <param name="configuration"></param>
public sealed class DependencyRegistrar(IServiceCollection services, IServiceInfo serviceInfo, IConfiguration configuration)
    : AbstractWebApiDependencyRegistrar(services, serviceInfo, configuration)
{
    public override void AddAdncServices()
    {
        services.AddXWSharedApplicationInfrastructure(serviceInfo, configuration);
        services.AddXWApplicationModules(serviceInfo, configuration, [.. ModuleCatalog.ApplicationRegistrarTypes]);
        services.AddXWModuleUnitOfWork();
        services.AddXWMessageTrackerServices(configuration);

        AddWebApiDefaultServices();
        services.AddInternalApiKeyAuth(configuration);

        var mysqlSection = configuration.GetRequiredSection(NodeConsts.Mysql);
        var redisSecton = configuration.GetRequiredSection(NodeConsts.Redis);
        var rabbitSecton = configuration.GetRequiredSection(NodeConsts.RabbitMq);
        var clientProvidedName = serviceInfo.Id;
        services.AddHealthChecks(checksBuilder =>
        {
            checksBuilder
                .AddMySql(mysqlSection)
                .AddRedis(redisSecton)
                .AddRabbitMQ(rabbitSecton, clientProvidedName);
        });
    }

    protected override IEnumerable<Assembly> GetModuleApiAssemblies() => ModuleCatalog.ApiAssemblies;
}
