namespace XW.Shared.Domain.Entities;

/// <summary>创建审计。</summary>
public interface IBasicAuditInfo
{
    /// <summary>创建人。</summary>
    long CreateBy { get; set; }

    /// <summary>创建时间。</summary>
    DateTime CreateTime { get; set; }
}
