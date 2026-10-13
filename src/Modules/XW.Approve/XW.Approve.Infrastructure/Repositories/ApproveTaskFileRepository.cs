using XW.Approve.Domain.Entities;
using XW.Approve.Domain.Repositories;
using XW.Approve.Infrastructure.Persistence;

namespace XW.Approve.Infrastructure.Repositories;

internal sealed class ApproveTaskFileRepository(ApproveDbContext db) : IApproveTaskFileRepository
{
    /// <inheritdoc />
    public async Task AddAsync(ApproveTaskFile file, CancellationToken cancellationToken = default)
    {
        db.Set<ApproveTaskFile>().Add(file);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<ApproveTaskFile?> FindAsync(long id, CancellationToken cancellationToken = default) =>
        db.Set<ApproveTaskFile>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApproveTaskFile>> ListByTaskAsync(long taskId, CancellationToken cancellationToken = default) =>
        await db.Set<ApproveTaskFile>().AsNoTracking()
            .Where(x => x.TaskId == taskId)
            .OrderBy(x => x.CreateTime)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApproveTaskFile>> ListByInstancesAsync(
        IReadOnlyCollection<long> instanceIds,
        CancellationToken cancellationToken = default)
    {
        if (instanceIds.Count == 0)
        {
            return [];
        }

        return await db.Set<ApproveTaskFile>().AsNoTracking()
            .Where(x => instanceIds.Contains(x.InstanceId))
            .OrderBy(x => x.CreateTime)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> CountByTaskAsync(long taskId, CancellationToken cancellationToken = default) =>
        db.Set<ApproveTaskFile>().CountAsync(x => x.TaskId == taskId, cancellationToken);

    /// <inheritdoc />
    public async Task RemoveAsync(ApproveTaskFile file, CancellationToken cancellationToken = default)
    {
        db.Set<ApproveTaskFile>().Remove(file);
        await db.SaveChangesAsync(cancellationToken);
    }
}
