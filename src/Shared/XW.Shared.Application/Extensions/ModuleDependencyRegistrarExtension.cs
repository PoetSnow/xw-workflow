using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XW.Shared;
using XW.Shared.Application.Registrar;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// 单体 Host 批量注册各模块的 <see cref="AbstractApplicationDependencyRegistrar"/>。
/// XW 通过本扩展一次加载多个模块。
/// </summary>
public static class ModuleDependencyRegistrarExtension
{
    /// <summary>
    /// 按类型列表依次调用各模块的 <see cref="AbstractApplicationDependencyRegistrar.AddApplicationServices"/>。
    /// </summary>
    public static IServiceCollection AddXWApplicationModules(
        this IServiceCollection services,
        IServiceInfo serviceInfo,
        IConfiguration configuration,
        IEnumerable<Type> applicationRegistrarTypes)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(serviceInfo);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(applicationRegistrarTypes);

        var registrarTypes = applicationRegistrarTypes
            .Where(IsModuleApplicationRegistrar)
            .ToList();

        if (registrarTypes.Count == 0)
        {
            var provided = string.Join(", ", applicationRegistrarTypes.Select(t => t?.FullName ?? "(null)"));
            throw new InvalidOperationException(
                $"No {nameof(AbstractApplicationDependencyRegistrar)} types were provided. Types received: [{provided}]");
        }

        foreach (var registrarType in registrarTypes)
        {
            var registrar = CreateRegistrar(registrarType, services, serviceInfo, configuration);
            registrar.AddApplicationServices();
        }

        return services;
    }

    /// <summary>
    /// 从指定 Application 程序集中发现 <see cref="AbstractApplicationDependencyRegistrar"/> 并注册。
    /// </summary>
    public static IServiceCollection AddXWApplicationModules(
        this IServiceCollection services,
        IServiceInfo serviceInfo,
        IConfiguration configuration,
        params Assembly[] applicationAssemblies)
    {
        ArgumentNullException.ThrowIfNull(applicationAssemblies);

        var registrarTypes = applicationAssemblies
            .SelectMany(assembly => assembly.ExportedTypes)
            .Where(IsModuleApplicationRegistrar)
            .Distinct()
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();

        return services.AddXWApplicationModules(serviceInfo, configuration, registrarTypes);
    }

    private static bool IsModuleApplicationRegistrar(Type type)
        => type is { IsClass: true, IsAbstract: false }
           && typeof(AbstractApplicationDependencyRegistrar).IsAssignableFrom(type);

    private static AbstractApplicationDependencyRegistrar CreateRegistrar(
        Type registrarType,
        IServiceCollection services,
        IServiceInfo serviceInfo,
        IConfiguration configuration)
    {
        var instance = Activator.CreateInstance(
            registrarType,
            services,
            serviceInfo,
            configuration,
            ServiceLifetime.Scoped);
        return instance as AbstractApplicationDependencyRegistrar
            ?? throw new InvalidOperationException($"Unable to create {registrarType.FullName}.");
    }
}
