using XW.Approve.Domain.Ports;
using XW.Shared.Application.Contracts.Integrations;

namespace XW.Approve.Infrastructure.Readers;

internal sealed class ApproveActorDirectory(IOrgDirectory orgDirectory) : IApproveActorDirectory
{
    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<long, string>> GetUserNamesAsync(
        IReadOnlyCollection<long> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<long, string>();
        }

        var users = await orgDirectory.ListUsersAsync(userIds.Distinct().ToList(), cancellationToken);
        return users.ToDictionary(x => x.Id, x => string.IsNullOrWhiteSpace(x.Name) ? x.Account : x.Name);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<long, string>> GetPositionNamesAsync(
        IReadOnlyCollection<long> positionIds,
        CancellationToken cancellationToken = default)
    {
        if (positionIds.Count == 0)
        {
            return new Dictionary<long, string>();
        }

        var wanted = positionIds.ToHashSet();
        var positions = await orgDirectory.ListEnabledPositionsAsync(cancellationToken);
        return positions
            .Where(x => wanted.Contains(x.Id))
            .ToDictionary(x => x.Id, x => x.Name);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<long, string>> GetDeptNamesAsync(
        IReadOnlyCollection<long> deptIds,
        CancellationToken cancellationToken = default)
    {
        if (deptIds.Count == 0)
        {
            return new Dictionary<long, string>();
        }

        var wanted = deptIds.ToHashSet();
        var depts = await orgDirectory.ListEnabledDeptsAsync(cancellationToken);
        return depts
            .Where(x => wanted.Contains(x.Id))
            .ToDictionary(x => x.Id, x => x.Name);
    }
}
