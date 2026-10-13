using Microsoft.Extensions.DependencyInjection;
using XW.Infra.Repository;
using XW.Host.API.Infrastructure;
using XW.Infra.Helper;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Host 级模块事务（<see cref="IModuleUnitOfWork"/>）DI 注册扩展。
/// </summary>
public static class ModuleUnitOfWorkRegistrationExtension
{
    /// <summary>
    /// 注册同库多 DbContext 事务实现 <see cref="MySqlModuleUnitOfWork"/>。
    /// <para>须在各类模块 DbContext 注册完成之后、Host 启动管道之前调用（见 <c>DependencyRegistrar</c>）。</para>
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>同一 <paramref name="services"/> 实例，便于链式调用。</returns>
    public static IServiceCollection AddXWModuleUnitOfWork(this IServiceCollection services)
    {
        if (services.HasRegistered(nameof(AddXWModuleUnitOfWork)))
        {
            return services;
        }

        services.AddScoped<IModuleUnitOfWork, MySqlModuleUnitOfWork>();
        return services;
    }
}
