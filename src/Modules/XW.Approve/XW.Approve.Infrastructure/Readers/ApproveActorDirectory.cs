using XW.Approve.Domain.Ports;
using XW.System.Application.Contracts.Interfaces;

namespace XW.Approve.Infrastructure.Readers;

internal sealed class ApproveActorDirectory(
    IUserService userService,
    IPositionService positionService,
    IOrganizationService organizationService) : IApproveActorDirectory
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

        var users = await userService.GetManyAsync(userIds.Distinct().ToList());
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
        var positions = await positionService.GetEnabledListAsync();
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
        var orgs = await organizationService.GetEnabledListAsync();
        return orgs
            .Where(x => wanted.Contains(x.Id))
            .ToDictionary(x => x.Id, x => x.Name);
    }
}
