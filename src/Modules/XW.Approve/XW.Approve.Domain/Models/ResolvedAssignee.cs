namespace XW.Approve.Domain.Models;

/// <summary>解析出的审批人。</summary>
public sealed class ResolvedAssignee
{
    /// <summary>用户 Id，对应 sys_user.id。</summary>
    public long? UserId { get; init; }

    /// <summary>岗位 Id，对应 sys_position.id。</summary>
    public long? PositionId { get; init; }

    /// <summary>解析时部门 Id，对应 sys_organization.id。</summary>
    public long? DeptId { get; init; }

    /// <summary>按用户创建。</summary>
    public static ResolvedAssignee ForUser(long userId, long? positionId = null, long? deptId = null) =>
        new() { UserId = userId, PositionId = positionId, DeptId = deptId };
}
