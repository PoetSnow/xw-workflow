using System.Reflection;

namespace XW.Host.API.Modules;

/// <summary>
/// 模块目录：用于注册模块的依赖注入器和扫描模块的程序集。
/// </summary>
public static class ModuleCatalog
{
    /// <summary>
    /// Application-layer dependency registrars (one per module).
    /// </summary>
    public static IReadOnlyList<Type> ApplicationRegistrarTypes { get; } =
    [
        typeof(XW.System.Application.DependencyRegistrar),
      
        typeof(XW.Approve.Infrastructure.DependencyRegistrar),
        typeof(XW.Approve.Application.DependencyRegistrar),
       
    ];

    /// <summary>
    /// Application assemblies scanned for <see cref="XW.Shared.Application.Registrar.AbstractApplicationDependencyRegistrar"/>.
    /// </summary>
    public static IReadOnlyList<Assembly> ApplicationAssemblies { get; } =
    [
        typeof(XW.System.Application.DependencyRegistrar).Assembly,
        typeof(XW.Approve.Application.DependencyRegistrar).Assembly,
    ];

    /// <summary>
    /// 注册API接口
    /// </summary>
    public static IReadOnlyList<Assembly> ApiAssemblies { get; } =
    [
        typeof(XW.System.API.Controllers.AccountController).Assembly,
      
        typeof(XW.Approve.API.Controllers.ApproveInstancesController).Assembly,
    ];
}
