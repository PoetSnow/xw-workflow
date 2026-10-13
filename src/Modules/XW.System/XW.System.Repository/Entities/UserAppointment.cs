using XW.Infra.Repository;

namespace XW.System.Repository.Entities;

/// <summary>用户任职：人 × 部门 × 岗位。</summary>
public class UserAppointment : EfFullAuditEntity
{
    /// <summary>用户 Id。</summary>
    public long UserId { get; set; }

    /// <summary>部门 Id。</summary>
    public long DeptId { get; set; }

    /// <summary>岗位 Id。</summary>
    public long PositionId { get; set; }

    /// <summary>是否主职。每人恰好一份主职。</summary>
    public bool IsPrimary { get; set; }

    /// <summary>是否有效。</summary>
    public bool Status { get; set; }

    /// <summary>生效开始，空表示不限。</summary>
    public DateTime? EffectiveStart { get; set; }

    /// <summary>生效结束，空表示不限。</summary>
    public DateTime? EffectiveEnd { get; set; }
}
