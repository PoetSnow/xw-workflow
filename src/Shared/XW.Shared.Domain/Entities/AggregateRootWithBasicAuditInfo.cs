namespace XW.Shared.Domain.Entities;

/// <summary>带创建审计字段的聚合根。</summary>
public class AggregateRootWithBasicAuditInfo : AggregateRoot, IBasicAuditInfo
{
    /// <summary>创建人。</summary>
    public long CreateBy { get; set; }

    /// <summary>创建时间。</summary>
    public DateTime CreateTime { get; set; }
}
