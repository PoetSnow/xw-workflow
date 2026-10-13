using XW.Shared.Application.Contracts.Integrations;
using XW.System.Application.Contracts.Dtos.User;
using XW.System.Application.Contracts.Interfaces;

namespace XW.System.Application.Services;

/// <summary>把 System 现有查询适配成跨模块只读目录。</summary>
public sealed class OrgDirectory(
    IUserService userService,
    IPositionService positionService,
    IUserAppointmentService appointmentService,
    IOrganizationService organizationService) : IOrgDirectory
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<OrgPositionItem>> ListEnabledPositionsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var positions = await positionService.GetEnabledListAsync();
        return positions.Select(x => new OrgPositionItem
        {
            Id = x.Id,
            Code = x.Code,
            Name = x.Name,
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OrgAppointmentItem>> ListActiveAppointmentsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var appointments = await appointmentService.GetActiveListAsync();
        return appointments.Select(ToAppointment).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OrgAppointmentItem>> ListActiveAppointmentsByUserAsync(long userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var appointments = await appointmentService.GetActiveByUserAsync(userId);
        return appointments.Select(ToAppointment).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OrgDeptItem>> ListEnabledDeptsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var depts = await organizationService.GetEnabledListAsync();
        return depts.Select(x => new OrgDeptItem
        {
            Id = x.Id,
            ParentId = x.ParentId,
            Name = x.Name,
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<OrgUserItem?> FindUserAsync(long userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userService.GetAsync(userId);
        return user is null ? null : ToUser(user);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OrgUserItem>> ListUsersAsync(IReadOnlyCollection<long> userIds, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var users = await userService.GetManyAsync(userIds);
        return users.Select(ToUser).ToList();
    }

    private static OrgAppointmentItem ToAppointment(UserAppointmentDto source) => new()
    {
        UserId = source.UserId,
        DeptId = source.DeptId,
        PositionId = source.PositionId,
        IsPrimary = source.IsPrimary,
    };

    private static OrgUserItem ToUser(UserDto source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        Account = source.Account,
        DeptId = source.DeptId,
    };
}
