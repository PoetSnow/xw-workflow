namespace XW.Shared.Application.Contracts.Integrations;

/// <summary>组织目录只读查询。由 System 实现，其他模块只依赖本接口。</summary>
public interface IOrgDirectory
{
    /// <summary>启用中的岗位。</summary>
    Task<IReadOnlyList<OrgPositionItem>> ListEnabledPositionsAsync(CancellationToken cancellationToken = default);

    /// <summary>全部有效任职。</summary>
    Task<IReadOnlyList<OrgAppointmentItem>> ListActiveAppointmentsAsync(CancellationToken cancellationToken = default);

    /// <summary>某用户当前有效任职。</summary>
    Task<IReadOnlyList<OrgAppointmentItem>> ListActiveAppointmentsByUserAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>启用中的部门。</summary>
    Task<IReadOnlyList<OrgDeptItem>> ListEnabledDeptsAsync(CancellationToken cancellationToken = default);

    /// <summary>按主键取用户。不存在时返回 null。</summary>
    Task<OrgUserItem?> FindUserAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>按主键批量取用户。不存在的 Id 不返回。</summary>
    Task<IReadOnlyList<OrgUserItem>> ListUsersAsync(IReadOnlyCollection<long> userIds, CancellationToken cancellationToken = default);
}
