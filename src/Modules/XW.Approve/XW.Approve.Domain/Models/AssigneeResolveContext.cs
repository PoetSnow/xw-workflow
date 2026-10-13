namespace XW.Approve.Domain.Models;

/// <summary>找人时用的组织快照。岗位任职走部门树。</summary>
public sealed class AssigneeResolveContext
{
    /// <summary>岗位编码到岗位 Id。</summary>
    public IReadOnlyDictionary<string, long> PositionCodeToId { get; init; } =
        new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

    /// <summary>有效任职。</summary>
    public IReadOnlyList<OrgAppointment> Appointments { get; init; } = [];

    /// <summary>部门 Id 到父部门 Id。根为 0。</summary>
    public IReadOnlyDictionary<long, long> DeptParentMap { get; init; } = new Dictionary<long, long>();
}
