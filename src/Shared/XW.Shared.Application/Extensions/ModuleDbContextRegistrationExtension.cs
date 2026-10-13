using System.Reflection;
using Microsoft.EntityFrameworkCore;
using XW.Infra.Repository.EfCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using XW.Infra.Repository;
using XW.Infra.Repository.EfCore.MySql;
using XW.Infra.Repository.EfCore.MySql.Transaction;
using XW.Infra.Repository.Interceptor.Castle;
using XW.Shared;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// 模块化 MySQL DbContext 注册（同库、独立 Context 类型）。放在 Shared.Application 层，而非 Infra。
/// 扩展为单体 Host 多模块场景。
/// </summary>
public static class ModuleDbContextRegistrationExtension
{
    /// <summary>
    /// 为单个模块注册 <typeparamref name="TDbContext"/>、<typeparamref name="TEntityInfo"/>，
    /// 以及该模块程序集中 <see cref="EfEntity"/> 类型的 <see cref="IEfRepository{TEntity}"/>。
    /// 同时调用 <c>AddEfRepository</c>（定义于 Infra.Repository.EfCore）。
    /// </summary>
    /// <param name="registerDefaultUnitOfWork">为 true 时注册绑定本模块 DbContext 的默认 <see cref="IUnitOfWork"/>。</param>
    public static IServiceCollection AddXWModuleMySqlDbContext<TDbContext, TEntityInfo>(
        this IServiceCollection services,
        IConfiguration configuration,
        Assembly? entitiesAssembly = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        bool registerDefaultUnitOfWork = false,
        string? connectionStringKey = null)
        where TDbContext : MySqlDbContext
        where TEntityInfo : class, IEntityInfo, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        var registrationKey = $"xw.module.dbcontext.{typeof(TDbContext).FullName}";
        if (services.HasRegistered(registrationKey))
        {
            return services;
        }

        connectionStringKey ??= NodeConsts.Mysql_ConnectionString;

        var connectionString = configuration[connectionStringKey]
            ?? throw new InvalidOperationException($"{connectionStringKey} is not configured.");
        var versionString = configuration[NodeConsts.Mysql_ServerVersion] ?? "11.7.2";
        var serverTypeString = configuration[NodeConsts.Mysql_ServerType] ?? nameof(ServerType.MariaDb);
        if (!Enum.TryParse(serverTypeString, out ServerType serverType))
        {
            throw new InvalidOperationException($"Invalid {NodeConsts.Mysql_ServerType}: {serverTypeString}");
        }

        var serverVersion = ServerVersion.Create(new Version(versionString), serverType);
        var splittingBehavior = QuerySplittingBehavior.SplitQuery;

        services.AddSingleton<TEntityInfo>();

        services.AddDbContext<TDbContext>((_, optionsBuilder) =>
        {
            optionsBuilder.UseLowerCaseNamingConvention();
            optionsBuilder.UseMySql(connectionString, serverVersion, mySqlOptions =>
            {
                mySqlOptions
                    .MinBatchSize(4)
                    .UseQuerySplittingBehavior(splittingBehavior);
            });
        }, lifetime, ServiceLifetime.Singleton);

        foreach (var assembly in ResolveEntityAssemblies<TEntityInfo>())
        {
            RegisterModuleEfRepositories<TDbContext>(services, assembly, lifetime);
        }

        RegisterUowInterceptorsOnce(services, lifetime);
        services.AddEfRepository(lifetime);

        if (registerDefaultUnitOfWork)
        {
            // 单体多模块同库：后注册的模块 DbContext 覆盖默认 IUnitOfWork（如 SupplyChain 写操作须落在本模块事务内）
            services.RemoveAll(typeof(IUnitOfWork));
            services.Add(new ServiceDescriptor(typeof(IUnitOfWork), provider =>
            {
                var context = provider.GetRequiredService<TDbContext>();
                var logger = provider.GetService<ILogger<MySqlUnitOfWork<TDbContext>>>();
                var publisher = provider.GetService<DotNetCore.CAP.ICapPublisher>();
                return new MySqlUnitOfWork<TDbContext>(context, logger, publisher);
            }, lifetime));
        }

        return services;
    }

    /// <summary>
    /// 将 <see cref="IEfBasicRepository{TEntity}"/> 绑定到本模块的 <typeparamref name="TDbContext"/>。
    /// 在模块 <c>DependencyRegistrar</c> 中为 DDD 聚合根调用 <see cref="IEfBasicRepository{TEntity}"/>）。
    /// 单体多 DbContext 场景须使用 closed-generic 显式注册；<c>AddEfRepository</c>。
    /// </summary>
    public static IServiceCollection AddXWModuleEfBasicRepository<TDbContext, TEntity>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TDbContext : DbContext
        where TEntity : Entity, IEfEntity<long>
    {
        var repositoryInterface = typeof(IEfBasicRepository<>).MakeGenericType(typeof(TEntity));
        if (services.Any(descriptor => descriptor.ServiceType == repositoryInterface))
        {
            return services;
        }

        services.Add(new ServiceDescriptor(repositoryInterface, provider =>
        {
            var dbContext = provider.GetRequiredService<TDbContext>();
            return new EfBasicRepository<TEntity>(dbContext);
        }, lifetime));

        return services;
    }

    /// <summary>
    /// 扫描程序集中 <see cref="EfEntity"/> 子类，将 <see cref="IEfRepository{TEntity}"/> 绑定到本模块 <typeparamref name="TDbContext"/>。
    /// XW 单体多 DbContext 必需
    /// </summary>
    private static void RegisterModuleEfRepositories<TDbContext>(
        IServiceCollection services,
        Assembly entitiesAssembly,
        ServiceLifetime lifetime)
        where TDbContext : DbContext
    {
        var entityTypes = entitiesAssembly.GetExportedTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(EfEntity).IsAssignableFrom(type));

        foreach (var entityType in entityTypes)
        {
            var repositoryInterface = typeof(IEfRepository<>).MakeGenericType(entityType);
            if (services.Any(descriptor => descriptor.ServiceType == repositoryInterface))
            {
                continue;
            }

            var repositoryImplementation = typeof(EfRepository<>).MakeGenericType(entityType);
            services.Add(new ServiceDescriptor(repositoryInterface, provider =>
            {
                var dbContext = provider.GetRequiredService<TDbContext>();
                var operater = provider.GetRequiredService<Operater>();
                var adoQuerier = provider.GetService<IAdoQuerierRepository>();
                return Activator.CreateInstance(repositoryImplementation, dbContext, operater, adoQuerier)
                    ?? throw new InvalidOperationException($"Unable to create repository for {entityType.Name}.");
            }, lifetime));
        }
    }

    /// <summary>
    /// 从 <typeparamref name="TEntityInfo"/> 解析实体所在程序集（含 Domain + Infrastructure 扫描配置）。
    /// </summary>
    private static IEnumerable<Assembly> ResolveEntityAssemblies<TEntityInfo>()
        where TEntityInfo : class, IEntityInfo, new()
    {
        var entityInfo = new TEntityInfo();
        if (entityInfo is AbstractEntityInfo abstractEntityInfo)
        {
            var method = typeof(AbstractEntityInfo).GetMethod(
                "GetEntityAssemblies",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method?.Invoke(abstractEntityInfo, null) is List<Assembly> assemblies)
            {
                return assemblies.Distinct();
            }
        }

        return [entityInfo.GetType().Assembly];
    }

    /// <summary>
    /// 全局只注册一次 UoW 拦截器（多模块共用）。
    /// </summary>
    private static void RegisterUowInterceptorsOnce(IServiceCollection services, ServiceLifetime lifetime)
    {
        if (services.HasRegistered("xw.module.uow.interceptors"))
        {
            return;
        }

        services.AddUowInterceptor(lifetime);
    }
}
