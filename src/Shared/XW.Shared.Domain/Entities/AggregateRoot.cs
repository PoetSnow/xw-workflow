namespace XW.Shared.Domain.Entities;

/// <summary>聚合根。不发布事件，也不持有基础设施服务。</summary>
public abstract class AggregateRoot : DomainEntity
{
}
