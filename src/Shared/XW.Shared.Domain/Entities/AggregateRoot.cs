using Microsoft.Extensions.DependencyInjection;
using XW.Infra.Core.DependencyInjection;
using XW.Infra.EventBus;
using XW.Infra.Repository;

namespace XW.Shared.Domain.Entities;

public abstract class AggregateRoot : DomainEntity, IConcurrency, IEfEntity<long>
{
    public Lazy<IEventPublisher> EventPublisher => new(() =>
    {
        return ServiceLocator.GetProvider().GetRequiredService<IEventPublisher>();
    });

    public byte[] RowVersion { get; set; } = [];
}
