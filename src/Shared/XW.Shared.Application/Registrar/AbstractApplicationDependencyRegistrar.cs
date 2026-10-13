using XW.Infra.EventBus.Tracker;
using XW.Infra.Repository;
using XW.Infra.Repository.EfCore;
using XW.Infra.Repository.EfCore.MySql;
using XW.Infra.Repository.Interceptor.Castle;
using XW.Shared.Application.Mapper.AutoMapper;
using XW.Shared.Application.Services.Trackers;
using Castle.DynamicProxy.Internal;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using XW.Shared.Application.Interceptors;
using XW.Infra.Redis.Caching.Core.Interceptor.Castle;
using Microsoft.Extensions.Configuration;
using XW.Shared.Application.Mapper;
using FluentValidation;
using XW.Shared.Application.Contracts.Interfaces;
using Castle.DynamicProxy;

namespace XW.Shared.Application.Registrar;

/// <summary>
/// 模块 Application 层依赖注册基类。
///  微服务：子类在 <see cref="AddApplicationServices"/> 中调用 <see cref="AddApplicaitonDefaultServices"/> 一次完成全部注册。
/// XW 单体：Host 已注册共享基础设施时，子类应使用 <see cref="AddModuleMySqlDbContext{TDbContext, TEntityInfo}"/> + <see cref="AddModuleApplicationServices"/>。
/// </summary>
public abstract partial class AbstractApplicationDependencyRegistrar
{
    public AbstractApplicationDependencyRegistrar(IServiceCollection services, IServiceInfo serviceInfo, IConfiguration configuration, ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(services, $"{nameof(IServiceCollection)} is null.");
        ArgumentNullException.ThrowIfNull(serviceInfo, $"{nameof(IServiceInfo)} is null.");
        ArgumentNullException.ThrowIfNull(serviceLifetime, $"{nameof(ServiceLifetime)} is null.");
        ArgumentNullException.ThrowIfNull(configuration, $"{nameof(IConfiguration)} is null.");

        Services = services;
        ServiceInfo = serviceInfo;
        Lifetime = serviceLifetime;
        Configuration = configuration;
    }

    public string Name => "application";

    /// <summary>本模块 Application 程序集（AppService、Validator、AutoMapper Profile 等）。</summary>
    protected abstract Assembly ApplicationLayerAssembly { get; }

    //public abstract Assembly ContractsLayerAssembly { get; }

    /// <summary>本模块 Repository 或 Domain 程序集（EntityInfo、领域服务等所在程序集）。</summary>
    protected abstract Assembly RepositoryOrDomainLayerAssembly { get; }

    /// <summary>AppService 动态代理默认拦截器：操作日志、缓存、工作单元。</summary>
    protected List<Type> DefaultInterceptorTypes => [typeof(OperateLogInterceptor), typeof(CachingInterceptor), typeof(UowInterceptor)];

    internal IServiceCollection Services { get; init; }
    internal IConfiguration Configuration { get; init; }
    internal IServiceInfo ServiceInfo { get; init; }
    internal ServiceLifetime Lifetime { get; init; }

    /// <summary>
    /// 模块入口：注册本模块全部 Application 服务（由 <see cref="ModuleDependencyRegistrarExtension"/> 调用）。
    /// </summary>
    public abstract void AddApplicationServices();

    /// <summary>
    /// 注册 默认 Application 服务（单 DbContext 微服务模式）。
    /// 含：MessageTracker、EF、Redis 缓存、AppService 代理等。单体多模块 Host 不应在各模块重复调用。
    /// </summary>
    protected void AddApplicaitonDefaultServices()
    {
        Services.TryAddSingleton(ServiceInfo);

        Services.TryAddSingleton(typeof(Lazy<>));
        Services.Add(new ServiceDescriptor(typeof(UserContext), typeof(UserContext), Lifetime));

        Services.Add(new ServiceDescriptor(typeof(IMessageTracker), typeof(DbMessageTrackerService), Lifetime));
        Services.Add(new ServiceDescriptor(typeof(IMessageTracker), typeof(RedisMessageTrackerService), Lifetime));
        Services.Add(new ServiceDescriptor(typeof(MessageTrackerFactory), typeof(MessageTrackerFactory), Lifetime));

        Services.AddHostedService<Channels.LogConsumersHostedService>();

        Services.Add(new ServiceDescriptor(typeof(OperateLogInterceptor), typeof(OperateLogInterceptor), Lifetime));
        Services.Add(new ServiceDescriptor(typeof(OperateLogAsyncInterceptor), typeof(OperateLogAsyncInterceptor), Lifetime));

        var redisSection = Configuration.GetRequiredSection(NodeConsts.Redis);
        var cachingSection = Configuration.GetRequiredSection(NodeConsts.Caching);
        //var consulSection = Configuration.GetRequiredSection(NodeConsts.Consul);
        Services
            .AddSingleton<IObjectMapper, AutoMapperObject>()
            .AddAutoMapper(cfg => { }, ApplicationLayerAssembly)
            .AddValidatorsFromAssembly(ApplicationLayerAssembly, Lifetime)
            .AddAdncInfraYitterIdGenerater(redisSection, ServiceInfo.ShortName.Split('-')[0], Lifetime)
            //.AddAdncInfraConsul(consulSection, null, Lifetime)
            .AddAdncInfraRedisCaching(ApplicationLayerAssembly, redisSection, cachingSection, Lifetime)
            .AddAdncInfraDapper(Lifetime);

        AddEfCoreContext();

        RegisterAppServices();
    }

    /// <summary>
    /// 注册模块自有 <see cref="MySqlDbContext"/>（同连接串、独立 Context 类型）。
    /// 须先在 Host 调用 <c>AddXWSharedApplicationInfrastructure</c>。
    /// 使用本方法时不要调用 <c>AddApplicaitonDefaultServices</c>。
    /// </summary>
    protected void AddModuleMySqlDbContext<TDbContext, TEntityInfo>(bool registerDefaultUnitOfWork = false)
        where TDbContext : MySqlDbContext
        where TEntityInfo : class, IEntityInfo, new()
    {
        Services.AddXWModuleMySqlDbContext<TDbContext, TEntityInfo>(
            Configuration,
            RepositoryOrDomainLayerAssembly,
            Lifetime,
            registerDefaultUnitOfWork);
    }

    /// <summary>
    /// 仅注册模块私有 Application 服务（AppService、Validator、AutoMapper、模块 Redis 缓存）。
    /// 共享基础设施须在 Host 层注册一次。
    /// </summary>
    public void AddModuleApplicationServices()
    {
        var redisSection = Configuration.GetRequiredSection(NodeConsts.Redis);
        var cachingSection = Configuration.GetRequiredSection(NodeConsts.Caching);

        Services
            .AddAutoMapper(cfg => { }, ApplicationLayerAssembly)
            .AddValidatorsFromAssembly(ApplicationLayerAssembly, Lifetime)
            .AddAdncInfraRedisCaching(ApplicationLayerAssembly, redisSection, cachingSection, Lifetime);

        RegisterAppServices();
    }

    /// <summary>
    /// 扫描并注册 <see cref="IAppService"/> 实现，通过 Castle 动态代理挂载拦截器。
    /// </summary>
    private void RegisterAppServices()
    {
        var implTypes = ApplicationLayerAssembly.GetExportedTypes().Where(type => type.IsClass && type.IsNotAbstractClass(true) && type.IsAssignableTo(typeof(IAppService))).ToList();
        implTypes?.ForEach(implType =>
        {
            var allInterfaces = implType.GetAllInterfaces().ToList();
            // Castle GetAllInterfaces 按 FullName 排序，Shared 中的 IAppService 可能排在模块契约接口之前
            var serviceType = allInterfaces.FirstOrDefault(x => x != typeof(IAppService) && x.IsAssignableTo(typeof(IAppService)))
                ?? allInterfaces.FirstOrDefault(x => x == typeof(IAppService));
            if (serviceType is not null)
            {
                Services.TryAddSingleton(new ProxyGenerator());
                Services.Add(new ServiceDescriptor(implType, implType, Lifetime));
                Services.Add(new ServiceDescriptor(serviceType, provider =>
                {
                    var interfaceToProxy = serviceType;
                    var target = provider.GetRequiredService(implType);
                    var interceptors = DefaultInterceptorTypes.ConvertAll(interceptorType => provider.GetService(interceptorType) as IInterceptor).ToArray();
                    var proxyGenerator = provider.GetRequiredService<ProxyGenerator>();
                    var proxy = proxyGenerator.CreateInterfaceProxyWithTargetInterface(interfaceToProxy, target, interceptors);
                    return proxy;
                }, Lifetime));
            }
        });
    }
}
