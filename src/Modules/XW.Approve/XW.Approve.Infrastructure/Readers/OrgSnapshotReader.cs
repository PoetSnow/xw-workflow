using XW.Approve.Domain.Models;
using XW.Approve.Domain.Ports;
using XW.System.Application.Contracts.Interfaces;

namespace XW.Approve.Infrastructure.Readers;

/// <summary>组织快照：岗位任职 + 部门树。</summary>
internal sealed class OrgSnapshotReader(
    IUserService userService,
    IPositionService positionService,
    IUserAppointmentService appointmentService,
    IOrganizationService organizationService) : IOrgSnapshotReader
{
    /// <inheritdoc />
    public async Task<AssigneeResolveContext> LoadAsync(CancellationToken cancellationToken = default)
    {
        var positions = await positionService.GetEnabledListAsync();
        var positionCodeToId = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var position in positions)
        {
            if (!string.IsNullOrWhiteSpace(position.Code))
            {
                positionCodeToId[position.Code] = position.Id;
            }
        }

        var appointments = (await appointmentService.GetActiveListAsync())
            .Select(x => new OrgAppointment(x.UserId, x.DeptId, x.PositionId))
            .ToList();

        var orgs = await organizationService.GetEnabledListAsync();
        var deptParentMap = orgs.ToDictionary(x => x.Id, x => x.ParentId);

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
        var appointments = await appointmentService.GetActiveByUserAsync(userId);
        var primary = appointments.FirstOrDefault(x => x.IsPrimary) ?? appointments.FirstOrDefault();
        if (primary is not null)
        {
            return primary.DeptId;
        }

        var user = await userService.GetAsync(userId);
        return user is null ? null : user.DeptId;
    }
}
