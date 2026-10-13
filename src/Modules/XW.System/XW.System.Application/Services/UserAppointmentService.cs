using XW.System.Application.Contracts.Dtos.User;

namespace XW.System.Application.Services;

/// <inheritdoc cref="IUserAppointmentService"/>
public class UserAppointmentService(
    IEfRepository<UserAppointment> appointmentRepo,
    IEfRepository<User> userRepo,
    IEfRepository<Position> positionRepo,
    CacheService cacheService) : AbstractAppService, IUserAppointmentService
{
    /// <inheritdoc />
    public async Task<List<UserAppointmentDto>> GetByUserAsync(long userId) =>
        await MapAsync(await appointmentRepo.Where(x => x.UserId == userId).ToListAsync());

    /// <inheritdoc />
    public async Task<List<UserAppointmentDto>> GetActiveByUserAsync(long userId)
    {
        var now = DateTime.Now;
        var rows = await appointmentRepo
            .Where(x => x.UserId == userId && x.Status)
            .ToListAsync();
        return await MapAsync(rows.Where(x => IsEffective(x, now)).ToList());
    }

    /// <inheritdoc />
    public async Task<List<UserAppointmentDto>> GetActiveListAsync()
    {
        var now = DateTime.Now;
        var rows = await appointmentRepo.Where(x => x.Status).ToListAsync();
        return await MapAsync(rows.Where(x => IsEffective(x, now)).ToList());
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ReplaceByUserAsync(long userId, List<UserAppointmentDto> input)
    {
        var user = await userRepo.FetchAsync(x => x.Id == userId, noTracking: false);
        if (user is null)
        {
            return Problem(HttpStatusCode.BadRequest, "用户不存在");
        }

        var error = Validate(input ?? []);
        if (error is not null)
        {
            return Problem(HttpStatusCode.BadRequest, error);
        }

        await appointmentRepo.ExecuteDeleteAsync(x => x.UserId == userId);
        var rows = (input ?? []).Select(x => new UserAppointment
        {
            Id = IdGenerater.GetNextId(),
            UserId = userId,
            DeptId = x.DeptId,
            PositionId = x.PositionId,
            IsPrimary = x.IsPrimary,
            Status = x.Status,
            EffectiveStart = x.EffectiveStart,
            EffectiveEnd = x.EffectiveEnd,
        }).ToList();
        if (rows.Count > 0)
        {
            await appointmentRepo.InsertRangeAsync(rows);
        }

        var primary = rows.FirstOrDefault(x => x.IsPrimary && x.Status);
        if (primary is not null)
        {
            user.DeptId = primary.DeptId;
            await userRepo.UpdateAsync(user);
        }

        return ServiceResult();
    }

    internal static string? Validate(IReadOnlyList<UserAppointmentDto> input)
    {
        if (input.Count == 0)
        {
            return "至少保留一份任职";
        }

        if (input.Count(x => x.IsPrimary) != 1)
        {
            return "每人必须恰好一份主职";
        }

        if (input.Any(x => x.DeptId <= 0 || x.PositionId <= 0))
        {
            return "任职必须选择部门和岗位";
        }

        if (input.GroupBy(x => (x.DeptId, x.PositionId)).Any(g => g.Count() > 1))
        {
            return "同一部门同一岗位不能重复任职";
        }

        return null;
    }

    internal static bool IsEffective(UserAppointment row, DateTime now) =>
        (row.EffectiveStart is null || row.EffectiveStart <= now)
        && (row.EffectiveEnd is null || row.EffectiveEnd >= now);

    private async Task<List<UserAppointmentDto>> MapAsync(IReadOnlyList<UserAppointment> rows)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var depts = await cacheService.GetAllOrganizationsFromCacheAsync();
        var positions = await positionRepo.GetAll().ToListAsync();
        return rows
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.Id)
            .Select(x =>
            {
                var position = positions.FirstOrDefault(p => p.Id == x.PositionId);
                return new UserAppointmentDto
                {
                    Id = x.Id,
                    UserId = x.UserId,
                    DeptId = x.DeptId,
                    DeptName = depts.FirstOrDefault(d => d.Id == x.DeptId)?.Name ?? string.Empty,
                    PositionId = x.PositionId,
                    PositionCode = position?.Code ?? string.Empty,
                    PositionName = position?.Name ?? string.Empty,
                    IsPrimary = x.IsPrimary,
                    Status = x.Status,
                    EffectiveStart = x.EffectiveStart,
                    EffectiveEnd = x.EffectiveEnd,
                };
            })
            .ToList();
    }
}
