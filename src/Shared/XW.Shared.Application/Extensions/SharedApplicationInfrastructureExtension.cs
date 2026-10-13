using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XW.Infra.Repository;
using XW.Shared;
using XW.Shared.Application.Channels;
using XW.Shared.Application.Interceptors;
using XW.Shared.Application.Mapper;
using XW.Shared.Application.Mapper.AutoMapper;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Host 级共享 Application 基础设施（UserContext、IdGenerater、Dapper、操作日志拦截器等），不含 EF Core。
///  <c>AddApplicaitonDefaultServices</c> 中与模块无关、且只需注册一次的部分。
/// 单体 Host 应先调用本方法，各模块再调用 <c>AddModuleApplicationServices</c>。
/// </summary>
public static class SharedApplicationInfrastructureExtension
{
    /// <summary>
    /// 注册全进程共享的 Application 服务（幂等，重复调用会被跳过）。
    /// </summary>
    public static IServiceCollection AddXWSharedApplicationInfrastructure(
        this IServiceCollection services,
        IServiceInfo serviceInfo,
        IConfiguration configuration,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(serviceInfo);
        ArgumentNullException.ThrowIfNull(configuration);

        const string registrationKey = "xw.shared.application.infrastructure";
        if (services.HasRegistered(registrationKey))
        {
            return services;
        }

        services.TryAddSingleton(serviceInfo);
        services.TryAddSingleton(typeof(Lazy<>));
        services.Add(new ServiceDescriptor(typeof(UserContext), typeof(UserContext), lifetime));

        services.AddHostedService<LogConsumersHostedService>();

        services.Add(new ServiceDescriptor(typeof(OperateLogInterceptor), typeof(OperateLogInterceptor), lifetime));
        services.Add(new ServiceDescriptor(typeof(OperateLogAsyncInterceptor), typeof(OperateLogAsyncInterceptor), lifetime));

        var redisSection = configuration.GetRequiredSection(NodeConsts.Redis);
        var shortName = serviceInfo.ShortName.Split('-')[0];

        services
            .AddSingleton<IObjectMapper, AutoMapperObject>()
            .AddAdncInfraYitterIdGenerater(redisSection, shortName, lifetime)
            .AddAdncInfraDapper(lifetime);

        services.Add(new ServiceDescriptor(typeof(Operater), provider =>
        {
            var userContext = provider.GetRequiredService<UserContext>();
            return new Operater
            {
                Id = userContext.Id == 0 ? 1000000000000 : userContext.Id,
                Account = string.IsNullOrEmpty(userContext.Account) ? "system" : userContext.Account,
                Name = string.IsNullOrEmpty(userContext.Name) ? "system" : userContext.Name
            };
        }, lifetime));

        return services;
    }
}
