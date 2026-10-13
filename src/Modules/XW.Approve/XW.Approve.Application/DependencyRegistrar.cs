using XW.Approve.Domain.Aggregates;

namespace XW.Approve.Application;

public sealed class DependencyRegistrar(
    IServiceCollection services,
    IServiceInfo serviceInfo,
    IConfiguration configuration,
    ServiceLifetime lifetime = ServiceLifetime.Scoped)
    : AbstractApplicationDependencyRegistrar(services, serviceInfo, configuration, lifetime)
{
    protected override Assembly ApplicationLayerAssembly => Assembly.GetExecutingAssembly();

    protected override Assembly RepositoryOrDomainLayerAssembly => typeof(ApproveInstance).Assembly;

    public override void AddApplicationServices()
    {
        AddModuleApplicationServices();
    }
}
