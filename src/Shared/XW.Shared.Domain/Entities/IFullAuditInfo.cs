namespace XW.Shared.Domain.Entities;

/// <summary>创建与修改审计。</summary>
public interface IFullAuditInfo : IBasicAuditInfo
{
    /// <summary>最后修改人。</summary>
    long ModifyBy { get; set; }

    /// <summary>最后修改时间。</summary>
    DateTime ModifyTime { get; set; }
}
