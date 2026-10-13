using XW.Approve.Domain.Entities;

namespace XW.Approve.Domain.Repositories;

/// <summary>待办附件表。文件字节走 <see cref="Ports.IApproveTaskFileStore"/>。</summary>
public interface IApproveTaskFileRepository
{
    /// <summary>新增一条附件记录。</summary>
    Task AddAsync(ApproveTaskFile file, CancellationToken cancellationToken = default);

    /// <summary>按 Id 取附件。</summary>
    Task<ApproveTaskFile?> FindAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>某待办的附件。</summary>
    Task<IReadOnlyList<ApproveTaskFile>> ListByTaskAsync(long taskId, CancellationToken cancellationToken = default);

    /// <summary>若干实例下的全部附件。</summary>
    Task<IReadOnlyList<ApproveTaskFile>> ListByInstancesAsync(IReadOnlyCollection<long> instanceIds, CancellationToken cancellationToken = default);

    /// <summary>某待办的附件数。</summary>
    Task<int> CountByTaskAsync(long taskId, CancellationToken cancellationToken = default);

    /// <summary>删除记录。不删磁盘文件。</summary>
    Task RemoveAsync(ApproveTaskFile file, CancellationToken cancellationToken = default);
}
