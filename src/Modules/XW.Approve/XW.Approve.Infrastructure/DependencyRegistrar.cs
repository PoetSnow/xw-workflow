using XW.Approve.Domain.Aggregates;
using XW.Approve.Domain.Ports;
using XW.Approve.Domain.Queries;
using XW.Approve.Domain.Repositories;
using XW.Approve.Infrastructure.Files;
using XW.Approve.Infrastructure.Persistence;
using XW.Approve.Infrastructure.Queries;
using XW.Approve.Infrastructure.Readers;
using XW.Approve.Infrastructure.Repositories;

namespace XW.Approve.Infrastructure;

public sealed class DependencyRegistrar(
    IServiceCollection services,
    IServiceInfo serviceInfo,
    IConfiguration configuration,
    ServiceLifetime lifetime = ServiceLifetime.Scoped)
    : AbstractApplicationDependencyRegistrar(services, serviceInfo, configuration, lifetime)
{
    protected override Assembly ApplicationLayerAssembly => typeof(DependencyRegistrar).Assembly;

    protected override Assembly RepositoryOrDomainLayerAssembly => typeof(EntityInfo).Assembly;

    public override void AddApplicationServices()
    {
        services.AddScoped<IApproveInstanceRepository, ApproveInstanceRepository>();
        services.AddScoped<IApproveProcessGraphRepository, ApproveProcessGraphRepository>();
        services.AddScoped<IApproveProcessBindingRepository, ApproveProcessBindingRepository>();
        services.AddScoped<IApproveProcessDesignRepository, ApproveProcessDesignRepository>();
        services.AddScoped<IOrgSnapshotReader, OrgSnapshotReader>();
        services.AddScoped<IApproveActorDirectory, ApproveActorDirectory>();
        services.AddScoped<IApproveQuery, ApproveQuery>();
        services.AddScoped<IApproveTaskFileRepository, ApproveTaskFileRepository>();
        services.AddSingleton<IApproveTaskFileStore, LocalApproveTaskFileStore>();

        AddModuleMySqlDbContext<ApproveDbContext, EntityInfo>(registerDefaultUnitOfWork: true);
        services.AddXWModuleEfBasicRepository<ApproveDbContext, ApproveInstance>(lifetime);
    }
}
