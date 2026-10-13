using XW.Approve.Domain.Aggregates;
using XW.Approve.Domain.Constants;
using XW.Approve.Domain.Repositories;

namespace XW.Approve.Infrastructure.Repositories;

internal sealed class ApproveInstanceRepository(IEfBasicRepository<ApproveInstance> repo) : IApproveInstanceRepository
{
    private static readonly Expression<Func<ApproveInstance, dynamic>>[] Children =
    [
        x => x.Nodes,
        x => x.Tasks,
        x => x.Histories,
    ];

    /// <inheritdoc />
    public Task<ApproveInstance?> GetForWriteAsync(long id, CancellationToken cancellationToken = default) =>
        repo.GetAsync(id, Children, writeDb: true, cancellationToken: cancellationToken);

    /// <inheritdoc />
    public Task<ApproveInstance?> GetWithChildrenAsync(long id, CancellationToken cancellationToken = default) =>
        repo.GetAsync(id, Children, writeDb: false, cancellationToken: cancellationToken);

    /// <inheritdoc />
    public Task<ApproveInstance?> FindRunningAsync(
        string businessType,
        string businessId,
        CancellationToken cancellationToken = default) =>
        repo.Where(
                x => x.BusinessType == businessType
                     && x.BusinessId == businessId
                     && x.Status == ApproveInstanceStatus.Running,
                writeDb: true,
                noTracking: false)
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<ApproveInstance?> FindLatestByBusinessAsync(
        string businessType,
        string businessId,
        CancellationToken cancellationToken = default)
    {
        var id = await repo
            .Where(x => x.BusinessType == businessType && x.BusinessId == businessId)
            .OrderByDescending(x => x.StartTime)
            .ThenByDescending(x => x.Id)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return id == 0 ? null : await GetWithChildrenAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApproveInstance>> ListByBusinessAsync(
        string businessType,
        string businessId,
        CancellationToken cancellationToken = default)
    {
        var ids = await repo
            .Where(x => x.BusinessType == businessType && x.BusinessId == businessId)
            .OrderBy(x => x.StartTime)
            .ThenBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var list = new List<ApproveInstance>(ids.Count);
        foreach (var id in ids)
        {
            var instance = await GetWithChildrenAsync(id, cancellationToken);
            if (instance is not null)
            {
                list.Add(instance);
            }
        }

        return list;
    }

    /// <inheritdoc />
    public Task InsertAsync(ApproveInstance instance, CancellationToken cancellationToken = default) =>
        repo.InsertAsync(instance, cancellationToken);

    /// <inheritdoc />
    public Task UpdateAsync(ApproveInstance instance, CancellationToken cancellationToken = default) =>
        repo.UpdateAsync(instance, cancellationToken);

    /// <inheritdoc />
    public async Task<ApproveInstance?> GetByTaskIdForWriteAsync(long taskId, CancellationToken cancellationToken = default)
    {
        var instanceId = await repo
            .Where(x => x.Tasks.Any(t => t.Id == taskId), writeDb: true, noTracking: true)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return instanceId == 0
            ? null
            : await GetForWriteAsync(instanceId, cancellationToken);
    }
}
