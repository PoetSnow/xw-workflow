using XW.Approve.Domain.Constants;
using XW.Approve.Domain.Entities;
using XW.Approve.Domain.Models;
using XW.Approve.Domain.Repositories;
using XW.Approve.Infrastructure.Persistence;

namespace XW.Approve.Infrastructure.Repositories;

internal sealed class ApproveProcessGraphRepository(ApproveDbContext db) : IApproveProcessGraphRepository
{
    /// <inheritdoc />
    public Task<ProcessRuntimeGraph?> GetByProcessDefIdAsync(
        long processDefId,
        CancellationToken cancellationToken = default) =>
        LoadGraphAsync(processDefId, requireActive: false, cancellationToken);

    /// <inheritdoc />
    public Task<ProcessRuntimeGraph?> GetActiveByProcessDefIdAsync(
        long processDefId,
        CancellationToken cancellationToken = default) =>
        LoadGraphAsync(processDefId, requireActive: true, cancellationToken);

    private async Task<ProcessRuntimeGraph?> LoadGraphAsync(
        long processDefId,
        bool requireActive,
        CancellationToken cancellationToken)
    {
        var def = await db.Set<ApproveProcessDef>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == processDefId, cancellationToken);
        if (def is null)
        {
            return null;
        }

        if (requireActive && !ApproveToken.Equals(def.Status, ApproveProcessStatus.Active))
        {
            return null;
        }

        var nodes = await db.Set<ApproveProcessNode>()
            .AsNoTracking()
            .Where(x => x.ProcessDefId == processDefId)
            .OrderBy(x => x.Ordinal)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (nodes.Count == 0)
        {
            return null;
        }

        var transitions = await db.Set<ApproveProcessTransition>()
            .AsNoTracking()
            .Where(x => x.ProcessDefId == processDefId)
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var nodeIds = nodes.Select(x => x.Id).ToList();
        var resolvers = nodeIds.Count == 0
            ? []
            : await db.Set<ApproveResolver>()
                .AsNoTracking()
                .Where(x => nodeIds.Contains(x.ProcessNodeId))
                .OrderBy(x => x.Ordinal)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

        var transitionIds = transitions.Select(x => x.Id).ToList();
        var groups = transitionIds.Count == 0
            ? []
            : await db.Set<ApproveConditionGroup>()
                .AsNoTracking()
                .Where(x => x.OwnerType == ApproveConditionOwner.Transition
                            && transitionIds.Contains(x.OwnerId))
                .OrderBy(x => x.Ordinal)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

        var groupIds = groups.Select(x => x.Id).ToList();
        var items = groupIds.Count == 0
            ? []
            : await db.Set<ApproveConditionItem>()
                .AsNoTracking()
                .Where(x => groupIds.Contains(x.GroupId))
                .OrderBy(x => x.Ordinal)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

        return new ProcessRuntimeGraph
        {
            Nodes = nodes,
            Transitions = transitions,
            ConditionGroups = groups,
            ConditionItems = items,
            Resolvers = resolvers,
        };
    }
}
