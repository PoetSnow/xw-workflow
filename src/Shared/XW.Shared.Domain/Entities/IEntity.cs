namespace XW.Shared.Domain.Entities;

/// <summary>实体标识。持久化实现可以继承本接口，领域模型不依赖具体仓储。</summary>
public interface IEntity<TKey>
{
    /// <summary>主键。</summary>
    TKey Id { get; set; }
}
