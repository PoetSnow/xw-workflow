namespace XW.Shared.Domain.Entities;

/// <summary>带完整审计字段的聚合根。</summary>
public class AggregateRootWithFullAuditInfo : AggregateRoot, IFullAuditInfo
{
    /// <summary>创建人。</summary>
    public long CreateBy { get; set; }

    /// <summary>创建时间。</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>最后修改人。</summary>
    public long ModifyBy { get; set; }

    /// <summary>最后修改时间。</summary>
    public DateTime ModifyTime { get; set; }
}
