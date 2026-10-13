using XW.Shared.WebApi.Routing;
using XW.System.Application.Contracts.Dtos.Position;

namespace XW.System.API.Controllers;

/// <summary>岗位管理</summary>
[Route($"{ApiSurfaces.ConsoleRoutePrefix}/admin/positions")]
public class PositionController(IPositionService positionService) : ConsoleApiController
{
    /// <summary>创建岗位</summary>
    [HttpPost]
    [AdncAuthorize(PermissionConsts.Position.Create)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<IdDto>> CreateAsync([FromBody] PositionCreationDto input)
        => CreatedResult(await positionService.CreateAsync(input));

    /// <summary>更新岗位</summary>
    [HttpPut("{id}")]
    [AdncAuthorize(PermissionConsts.Position.Update)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> UpdateAsync([FromRoute] long id, [FromBody] PositionUpdationDto input)
        => Result(await positionService.UpdateAsync(id, input));

    /// <summary>删除岗位</summary>
    [HttpDelete("{ids}")]
    [AdncAuthorize(PermissionConsts.Position.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteAsync([FromRoute] string ids)
    {
        var idArr = ids.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(long.Parse).ToArray();
        return Result(await positionService.DeleteAsync(idArr));
    }

    /// <summary>分页查询岗位</summary>
    [HttpGet("page")]
    [AdncAuthorize(PermissionConsts.Position.Search)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PageModelDto<PositionDto>>> GetPagedAsync([FromQuery] SearchPagedDto input)
        => await positionService.GetPagedAsync(input);

    /// <summary>岗位选项</summary>
    [HttpGet("options")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OptionTreeDto>>> GetOptionsAsync()
        => await positionService.GetOptionsAsync(true);

    /// <summary>启用中的岗位（含编码），供审批设计器。</summary>
    [HttpGet("enabled")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PositionDto>>> GetEnabledListAsync()
        => await positionService.GetEnabledListAsync();

    /// <summary>按主键获取岗位</summary>
    [HttpGet("{id:long}")]
    [AdncAuthorize([PermissionConsts.Position.Get, PermissionConsts.Position.Update])]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PositionDto>> GetAsync([FromRoute] long id)
    {
        var dto = await positionService.GetAsync(id);
        return dto is null ? NotFound() : dto;
    }
}
