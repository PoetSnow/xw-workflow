namespace XW.Approve.Domain.Ports;

/// <summary>把用户 Id、岗位 Id、部门 Id 换成展示名。时间线用。</summary>
public interface IApproveActorDirectory
{
    /// <summary>用户 Id → 姓名。</summary>
    Task<IReadOnlyDictionary<long, string>> GetUserNamesAsync(
        IReadOnlyCollection<long> userIds,
        CancellationToken cancellationToken = default);

    /// <summary>岗位 Id → 名称。</summary>
    Task<IReadOnlyDictionary<long, string>> GetPositionNamesAsync(
        IReadOnlyCollection<long> positionIds,
        CancellationToken cancellationToken = default);

    /// <summary>部门 Id → 名称。</summary>
    Task<IReadOnlyDictionary<long, string>> GetDeptNamesAsync(
        IReadOnlyCollection<long> deptIds,
        CancellationToken cancellationToken = default);
}
