using XW.Infra.Repository;

namespace XW.Shared.Domain.Entities;

public class AggregateRootWithFullAuditInfo : AggregateRoot, IFullAuditInfo
{
    public long CreateBy { get; set; }
    public DateTime CreateTime { get; set; }
    public long ModifyBy { get; set; }
    public DateTime ModifyTime { get; set; }
}
