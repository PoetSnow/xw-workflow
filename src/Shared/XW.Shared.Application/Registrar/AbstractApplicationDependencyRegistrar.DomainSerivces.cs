using Microsoft.Extensions.DependencyInjection;

namespace XW.Shared.Application.Registrar;

public abstract partial class AbstractApplicationDependencyRegistrar
{
    /// <summary>
    /// 注册领域服务（扫描 RepositoryOrDomainLayerAssembly 中实现 <typeparamref name="TDomainService"/> 的类型）。
    /// </summary>
    protected virtual void AddDomainSerivces<TDomainService>()
        where TDomainService : class
    {
        var serviceType = typeof(TDomainService);
        var implTypes = RepositoryOrDomainLayerAssembly.ExportedTypes.Where(type => type.IsAssignableTo(serviceType) && type.IsNotAbstractClass(true)).ToList();
        implTypes?.ForEach(implType =>
        {
            Services.Add(new ServiceDescriptor(implType, implType, Lifetime));
        });
    }
}
