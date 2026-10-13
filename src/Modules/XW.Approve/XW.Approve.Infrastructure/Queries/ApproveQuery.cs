using XW.Approve.Domain.Aggregates;
using XW.Approve.Domain.Constants;
using XW.Approve.Domain.Queries;
using XW.Approve.Infrastructure.Persistence;

namespace XW.Approve.Infrastructure.Queries;

internal sealed class ApproveQuery(ApproveDbContext db) : IApproveQuery
{
    /// <inheritdoc />
    public async Task<PagedResult<ApproveTaskInboxItem>> GetMyPendingTasksAsync(
        long userId,
        string? keywords,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query =
            from task in db.Set<ApproveTask>().AsNoTracking()
            join instance in db.Set<ApproveInstance>().AsNoTracking() on task.InstanceId equals instance.Id
            join node in db.Set<ApproveInstanceNode>().AsNoTracking() on task.InstanceNodeId equals node.Id into nodeJoin
            from node in nodeJoin.DefaultIfEmpty()
            where task.AssigneeUserId == userId
                  && task.Status == ApproveTaskStatus.Pending
                  && instance.Status == ApproveInstanceStatus.Running
            select new { task, instance, node };

        if (!string.IsNullOrWhiteSpace(keywords))
        {
            var key = keywords.Trim();
            query = query.Where(x => x.instance.Title.Contains(key) || x.instance.BusinessId.Contains(key));
        }

        var total = await query.CountAsync(cancellationToken);
        if (total == 0)
        {
            return new PagedResult<ApproveTaskInboxItem> { Items = [], Total = 0 };
        }

        var rows = await query
            .OrderByDescending(x => x.task.CreateTime)
            .ThenByDescending(x => x.task.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return new PagedResult<ApproveTaskInboxItem>
        {
            Total = total,
            Items = rows.Select(x => new ApproveTaskInboxItem
            {
                Task = x.task,
                Instance = x.instance,
                Node = x.node,
            }).ToList(),
        };
    }

    /// <inheritdoc />
    public async Task<PagedResult<ApproveInstance>> GetMySubmittedAsync(
        long userId,
        string? keywords,
        string? status,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = db.Set<ApproveInstance>().AsNoTracking().Where(x => x.SubmitUserId == userId);
        if (!string.IsNullOrWhiteSpace(keywords))
        {
            var key = keywords.Trim();
            query = query.Where(x => x.Title.Contains(key) || x.BusinessId.Contains(key));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim();
            query = query.Where(x => x.Status == st);
        }

        var total = await query.CountAsync(cancellationToken);
        if (total == 0)
        {
            return new PagedResult<ApproveInstance> { Items = [], Total = 0 };
        }

        var items = await query
            .OrderByDescending(x => x.StartTime)
            .ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return new PagedResult<ApproveInstance> { Items = items, Total = total };
    }
}
