namespace XW.Infra.Repository;

/// <summary>创建与修改审计。成员定义在 <see cref="XW.Shared.Domain.Entities.IFullAuditInfo"/>。</summary>
public interface IFullAuditInfo : IBasicAuditInfo, XW.Shared.Domain.Entities.IFullAuditInfo
{
}
