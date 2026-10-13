namespace XW.Shared.Application.Contracts.Integrations;

/// <summary>任职只读项。</summary>
public sealed class OrgAppointmentItem
{
    /// <summary>用户 Id。</summary>
    public long UserId { get; set; }

    /// <summary>部门 Id，对应组织 Id。</summary>
    public long DeptId { get; set; }

    /// <summary>岗位 Id。</summary>
    public long PositionId { get; set; }

    /// <summary>是否主职。</summary>
    public bool IsPrimary { get; set; }
}
