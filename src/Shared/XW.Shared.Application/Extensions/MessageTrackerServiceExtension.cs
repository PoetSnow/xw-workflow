using Microsoft.Extensions.Configuration;
using XW.Infra.EventBus.Tracker;
using XW.Shared.Application.Services.Trackers;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// 注册 CAP 消息追踪器（幂等/去重）。须在模块 DbContext 与仓储注册完成之后调用。
/// </summary>
public static class MessageTrackerServiceExtension
{
    /// <summary>
    /// 注册数据库与 Redis 两种 <see cref="IMessageTracker"/> 实现及 <see cref="MessageTrackerFactory"/>。
    /// </summary>
    public static IServiceCollection AddXWMessageTrackerServices(
        this IServiceCollection services,
        IConfiguration configuration,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        _ = configuration;

        const string registrationKey = "xw.message.tracker.services";
        if (services.HasRegistered(registrationKey))
        {
            return services;
        }

        services.Add(new ServiceDescriptor(typeof(IMessageTracker), typeof(DbMessageTrackerService), lifetime));
        services.Add(new ServiceDescriptor(typeof(IMessageTracker), typeof(RedisMessageTrackerService), lifetime));
        services.Add(new ServiceDescriptor(typeof(MessageTrackerFactory), typeof(MessageTrackerFactory), lifetime));

        return services;
    }
}
