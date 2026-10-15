using System.Reflection;
using XW.Inspection.Domain;
using XW.Inspection.Infrastructure.Agent;
using XW.Inspection.Infrastructure.Persistence;

namespace XW.Inspection.Infrastructure;

/// <summary>注册检测项目的库和 Agent 客户端。</summary>
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
    public override void AddApplicationServices()
    {
        AddModuleMySqlDbContext<InspectionDbContext, EntityInfo>();
        services.AddScoped<IInspectionStore, InspectionStore>();
        services.AddHttpClient<IAgentInspectionClient, AgentInspectionClient>((_, client) =>
        {
            var url = configuration["Inspection:AgentBaseUrl"];
            if (!string.IsNullOrWhiteSpace(url))
            {
                client.BaseAddress = new Uri(url.TrimEnd('/') + "/");
            }

            client.Timeout = TimeSpan.FromMinutes(6);
        });
    }
}
