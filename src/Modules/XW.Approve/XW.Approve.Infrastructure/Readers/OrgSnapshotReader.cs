using XW.Approve.Domain.Models;
using XW.Approve.Domain.Ports;
using XW.Shared.Application.Contracts.Integrations;

namespace XW.Approve.Infrastructure.Readers;

/// <summary>组织快照：岗位任职 + 部门树。</summary>
internal sealed class OrgSnapshotReader(IOrgDirectory orgDirectory) : IOrgSnapshotReader
{
    /// <inheritdoc />
    public async Task<AssigneeResolveContext> LoadAsync(CancellationToken cancellationToken = default)
    {
        var positions = await orgDirectory.ListEnabledPositionsAsync(cancellationToken);
        var positionCodeToId = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var position in positions)
        {
            if (!string.IsNullOrWhiteSpace(position.Code))
            {
                positionCodeToId[position.Code] = position.Id;
            }
        }

        var appointments = (await orgDirectory.ListActiveAppointmentsAsync(cancellationToken))
            .Select(x => new OrgAppointment(x.UserId, x.DeptId, x.PositionId))
            .ToList();

        var depts = await orgDirectory.ListEnabledDeptsAsync(cancellationToken);
        var deptParentMap = depts.ToDictionary(x => x.Id, x => x.ParentId);

        return new AssigneeResolveContext
        {
            PositionCodeToId = positionCodeToId,
            Appointments = appointments,
            DeptParentMap = deptParentMap,
        };
    }

    /// <inheritdoc />
    public async Task<long?> GetUserDeptIdAsync(long userId, CancellationToken cancellationToken = default)
    {
        var appointments = await orgDirectory.ListActiveAppointmentsByUserAsync(userId, cancellationToken);
        var primary = appointments.FirstOrDefault(x => x.IsPrimary) ?? appointments.FirstOrDefault();
        if (primary is not null)
        {
            return primary.DeptId;
        }

        var user = await orgDirectory.FindUserAsync(userId, cancellationToken);
        return user is null ? null : user.DeptId;
    }
}
