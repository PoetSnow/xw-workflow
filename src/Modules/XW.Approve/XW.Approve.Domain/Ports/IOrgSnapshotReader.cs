using XW.Approve.Domain.Models;

namespace XW.Approve.Domain.Ports;

/// <summary>从现有 RBAC 读组织快照。不引用 System.Domain。</summary>
public interface IOrgSnapshotReader
{
    /// <summary>加载岗位任职、部门树，以及旧角色映射。</summary>
    Task<AssigneeResolveContext> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>读取用户主职部门 Id。没有任职时回退 sys_user.deptid。</summary>
    Task<long?> GetUserDeptIdAsync(long userId, CancellationToken cancellationToken = default);
}
