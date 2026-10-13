using XW.Approve.Domain.Constants;
using XW.Infra.Repository;

namespace XW.Approve.Domain.Entities;

/// <summary>条件组。OwnerType 见 ApproveConditionOwner，支持 And / Or 嵌套。</summary>
public class ApproveConditionGroup : EfFullAuditEntity
{
    /// <summary>归属类型最大长度。</summary>
    public static readonly int OwnerTypeMaxLength = 16;

    /// <summary>组名最大长度。</summary>
    public static readonly int GroupNameMaxLength = 64;

    /// <summary>组逻辑最大长度。</summary>
    public static readonly int GroupLogicMaxLength = 8;

    /// <summary>组类型最大长度。</summary>
    public static readonly int GroupTypeMaxLength = 16;

    /// <summary>归属类型，见 ApproveConditionOwner。</summary>
    public string OwnerType { get; set; } = string.Empty;

    /// <summary>归属 Id，连线 Id 或绑定 Id。</summary>
    public long OwnerId { get; set; }

    /// <summary>父组 Id，根为 null。</summary>
    public long? ParentGroupId { get; set; }

    /// <summary>组名。</summary>
    public string GroupName { get; set; } = string.Empty;

    /// <summary>组逻辑，见 ApproveGroupLogic。</summary>
    public string GroupLogic { get; set; } = ApproveGroupLogic.And;

    /// <summary>组类型，见 ApproveGroupType。</summary>
    public string GroupType { get; set; } = ApproveGroupType.Root;

    /// <summary>同层顺序。</summary>
    public int Ordinal { get; set; }
}
