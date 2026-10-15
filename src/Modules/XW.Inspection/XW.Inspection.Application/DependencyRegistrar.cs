using XW.Inspection.Domain;

namespace XW.Inspection.Application;

/// <summary>注册检测项目应用服务。</summary>
public sealed class DependencyRegistrar(
    IServiceCollection services,
    IServiceInfo serviceInfo,
    IConfiguration configuration,
    ServiceLifetime lifetime = ServiceLifetime.Scoped)
    : AbstractApplicationDependencyRegistrar(services, serviceInfo, configuration, lifetime)
{
    /// <inheritdoc />
    protected override Assembly ApplicationLayerAssembly => typeof(DependencyRegistrar).Assembly;

    /// <inheritdoc />
    protected override Assembly RepositoryOrDomainLayerAssembly => typeof(InspectionRegulation).Assembly;

    /// <inheritdoc />
    public override void AddApplicationServices() => AddModuleApplicationServices();
}
