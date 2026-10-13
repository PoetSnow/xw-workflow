using XW.System.Application.Contracts.Dtos.Position;

namespace XW.System.Application.Services;

/// <inheritdoc cref="IPositionService"/>
public class PositionService(IEfRepository<Position> positionRepo, IEfRepository<UserAppointment> appointmentRepo)
    : AbstractAppService, IPositionService
{
    /// <inheritdoc />
    public async Task<ServiceResult<IdDto>> CreateAsync(PositionCreationDto input)
    {
        input.TrimStringFields();
        if (await positionRepo.AnyAsync(x => x.Code == input.Code))
        {
            return Problem(HttpStatusCode.BadRequest, "岗位编码已存在");
        }

        if (await positionRepo.AnyAsync(x => x.Name == input.Name))
        {
            return Problem(HttpStatusCode.BadRequest, "岗位名称已存在");
        }

        var entity = Mapper.Map<Position>(input, IdGenerater.GetNextId());
        await positionRepo.InsertAsync(entity);
        return new IdDto(entity.Id);
    }

    /// <inheritdoc />
    public async Task<ServiceResult> UpdateAsync(long id, PositionUpdationDto input)
    {
        input.TrimStringFields();
        var entity = await positionRepo.FetchAsync(x => x.Id == id, noTracking: false);
        if (entity is null)
        {
            return Problem(HttpStatusCode.BadRequest, "岗位不存在");
        }

        if (await positionRepo.AnyAsync(x => x.Code == input.Code && x.Id != id))
        {
            return Problem(HttpStatusCode.BadRequest, "岗位编码已存在");
        }

        if (await positionRepo.AnyAsync(x => x.Name == input.Name && x.Id != id))
        {
            return Problem(HttpStatusCode.BadRequest, "岗位名称已存在");
        }

        Mapper.Map(input, entity);
        await positionRepo.UpdateAsync(entity);
        return ServiceResult();
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteAsync(long[] ids)
    {
        if (await appointmentRepo.AnyAsync(x => ids.Contains(x.PositionId)))
        {
            return Problem(HttpStatusCode.BadRequest, "岗位已有任职，不能删除");
        }

        await positionRepo.ExecuteDeleteAsync(x => ids.Contains(x.Id));
        return ServiceResult();
    }

    /// <inheritdoc />
    public async Task<PositionDto?> GetAsync(long id)
    {
        var entity = await positionRepo.FetchAsync(x => x.Id == id);
        return entity is null ? null : Mapper.Map<PositionDto>(entity);
    }

    /// <inheritdoc />
    public async Task<PageModelDto<PositionDto>> GetPagedAsync(SearchPagedDto input)
    {
        input.TrimStringFields();
        var whereExpression = ExpressionCreator
            .New<Position>()
            .AndIf(input.Keywords.IsNotNullOrWhiteSpace(), x => EF.Functions.Like(x.Name, $"{input.Keywords}%")
                || EF.Functions.Like(x.Code, $"{input.Keywords}%"));

        var total = await positionRepo.CountAsync(whereExpression);
        if (total == 0)
        {
            return new PageModelDto<PositionDto>(input);
        }

        var entities = await positionRepo
            .Where(whereExpression)
            .OrderBy(x => x.Ordinal)
            .ThenBy(x => x.Id)
            .Skip(input.SkipRows())
            .Take(input.PageSize)
            .ToListAsync();
        return new PageModelDto<PositionDto>(input, Mapper.Map<List<PositionDto>>(entities), total);
    }

    /// <inheritdoc />
    public async Task<List<OptionTreeDto>> GetOptionsAsync(bool? status = null)
    {
        var whereExpr = ExpressionCreator
            .New<Position>()
            .AndIf(status is not null, x => x.Status == status);
        return await positionRepo
            .Where(whereExpr)
            .OrderBy(x => x.Ordinal)
            .Select(x => new OptionTreeDto { Label = x.Name, Value = x.Id })
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<List<PositionDto>> GetEnabledListAsync()
    {
        var entities = await positionRepo.Where(x => x.Status).OrderBy(x => x.Ordinal).ThenBy(x => x.Id).ToListAsync();
        return Mapper.Map<List<PositionDto>>(entities);
    }
}
