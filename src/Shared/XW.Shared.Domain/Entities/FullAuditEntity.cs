namespace XW.Shared.Domain.Entities;

/// <summary>带完整审计字段的领域实体。审批配置实体使用，不进入 EF 基类。</summary>
public abstract class FullAuditEntity : DomainEntity, IFullAuditInfo
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
