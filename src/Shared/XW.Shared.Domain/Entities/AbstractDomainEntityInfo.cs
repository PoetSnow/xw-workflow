using XW.Infra.Repository;
using XW.Infra.Repository.EfCore;
using XW.Shared.Domain.Entities;
using XW.Shared.Repository.EfCoreEntities;
using System.Reflection;

namespace xw.Shared.Domain.Entities;

public abstract class AbstractDomainEntityInfo : AbstractEntityInfo, IEntityInfo
{
    protected override List<Type> GetEntityTypes(IEnumerable<Assembly> assemblies)
    {
        var typeList = assemblies.SelectMany(assembly => assembly.GetTypes()
                                                  .Where(m => m.FullName != null
                                                   && (typeof(AggregateRoot).IsAssignableFrom(m) || typeof(DomainEntity).IsAssignableFrom(m))
                                                   && !m.IsAbstract)) ?? [];
        return typeList.Append(typeof(EventTracker)).ToList();
    }
}
