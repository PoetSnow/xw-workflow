namespace XW.Approve.Domain.Models;

/// <summary>有效任职快照。</summary>
public sealed record OrgAppointment(long UserId, long DeptId, long PositionId);
