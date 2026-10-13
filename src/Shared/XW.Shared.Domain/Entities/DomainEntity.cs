namespace XW.Shared.Domain.Entities;

/// <summary>领域实体。只带主键，不依赖 EF。</summary>
public abstract class DomainEntity : IEntity<long>
{
    /// <summary>主键。</summary>
    public long Id { get; set; }
}
