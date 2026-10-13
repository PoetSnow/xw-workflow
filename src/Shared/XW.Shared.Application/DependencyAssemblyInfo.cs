using System.Reflection;
using System.Runtime.Loader;

namespace XW.Shared.Application;

/// <summary>
/// 根据 Application 程序集名推导 Contracts / Repository / Domain 程序集位置。
/// </summary>
public sealed class DependencyAssemblyInfo
{
    private readonly Assembly _applicationAssembly;
    private readonly string _assemblyNamePrefix;

    public DependencyAssemblyInfo(Assembly applicationAssembly)
    {
        _applicationAssembly = applicationAssembly;
        var assemblyName = _applicationAssembly.GetName().Name ?? throw new InvalidOperationException("Application assembly name cannot be null");
        _assemblyNamePrefix = assemblyName.Remove(assemblyName.LastIndexOf('.'));
    }

    /// <summary>Application 层程序集。</summary>
    public Assembly ApplicationLayerAssembly
    {
        get
        {
            //var assemblyName = $"{_assemblyNamePrefix}.Application";
            //var assembly = GetAssemblyByName(assemblyName);
            //return assembly ?? throw new InvalidOperationException($"Cannot find assembly with name {assemblyName}");
            return _applicationAssembly;
        }
    }

    /// <summary>Application.Contracts 程序集。</summary>
    public Assembly ContractLayerAssembly
    {
        get
        {
            var assemblyName = $"{_assemblyNamePrefix}.Application.Contracts";
            var assembly = GetAssemblyByName(assemblyName);
            return assembly ?? throw new InvalidOperationException($"Cannot find assembly with name {assemblyName}");
        }
    }

    /// <summary>Repository 程序集；不存在时回退到 Domain 程序集。</summary>
    public Assembly RepositoryOrDomainLayerAssembly
    {
        get
        {
            var assemblyName = $"{_assemblyNamePrefix}.Repository";
            var assembly = GetAssemblyByName(assemblyName);
            if (assembly is null)
            {
                assemblyName = $"{_assemblyNamePrefix}.Domain";
                assembly = GetAssemblyByName(assemblyName);
            }
            return assembly ?? throw new InvalidOperationException($"Cannot find assembly with name {assemblyName}");
        }
    }

    private Assembly? GetAssemblyByName(string name)
    {
        var assembly = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(a => a.GetName().Name == name);
        if (assembly is not null)
        {
            return assembly;
        }
        else
        {
            var referencedAssemblyName = _applicationAssembly.GetReferencedAssemblies().Where(x => x.Name == name).FirstOrDefault();
            if (referencedAssemblyName is not null)
            {
                assembly = AssemblyLoadContext.Default.LoadFromAssemblyName(referencedAssemblyName);
            }
            else
            {
                var candidatePath = Path.Combine(AppContext.BaseDirectory, name + ".dll");
                if (File.Exists(candidatePath))
                {
                    assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(candidatePath);
                }
            }
            return assembly;
        }
    }
}
