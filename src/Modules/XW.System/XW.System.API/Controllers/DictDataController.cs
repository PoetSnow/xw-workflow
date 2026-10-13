using XW.Shared.WebApi.Routing;
using XW.System.Application.Contracts.Dtos.Dict;

namespace XW.System.API.Controllers;

/// <summary>
/// 字典数据管理
/// </summary>
[Route($"{ApiSurfaces.ConsoleRoutePrefix}/admin/dictdatas")]
public class DictDataController(IDictDataService dictDataService) : ConsoleApiController
{
    /// <summary>
    /// 创建字典数据
    /// </summary>
    /// <param name="input">创建参数</param>
    /// <returns>新记录主键</returns>
    [HttpPost]
    [AdncAuthorize(PermissionConsts.DictData.Create)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<IdDto>> CreateAsync([FromBody] DictDataCreationDto input)
        => CreatedResult(await dictDataService.CreateAsync(input));

    /// <summary>
    /// 更新字典数据
    /// </summary>
    /// <param name="id">主键</param>
    /// <param name="input">更新参数</param>
    /// <returns>操作结果</returns>
    [HttpPut("{id}")]
    [AdncAuthorize(PermissionConsts.DictData.Update)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<long>> UpdateAsync([FromRoute] long id, [FromBody] DictDataUpdationDto input)
        => Result(await dictDataService.UpdateAsync(id, input));

    /// <summary>
    /// 删除字典数据
    /// </summary>
    /// <param name="ids">主键列表</param>
    /// <returns>操作结果</returns>
    [HttpDelete("{ids}")]
    [AdncAuthorize(PermissionConsts.DictData.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteAsync([FromRoute] string ids)
    {
        var idArr = ids.Split(',').Select(long.Parse).ToArray();
        return Result(await dictDataService.DeleteAsync(idArr));
    }

    /// <summary>
    /// 分页查询字典数据
    /// </summary>
    /// <param name="input">分页与关键字</param>
    /// <returns>分页结果</returns>
    [HttpGet("page")]
    [AdncAuthorize(PermissionConsts.DictData.Search)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PageModelDto<DictDto>>> GetPagedAsync([FromQuery] DictDataSearchPagedDto input)
        => await dictDataService.GetPagedAsync(input);

    /// <summary>
    /// 按主键获取字典数据
    /// </summary>
    /// <param name="id">主键</param>
    /// <returns>字典数据详情</returns>
    [HttpGet("{id}")]
    [AdncAuthorize([PermissionConsts.DictData.Get, PermissionConsts.DictData.Update])]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<DictDataDto>> GetAsync([FromRoute] long id)
    {
        var dictData = await dictDataService.GetAsync(id);
        return dictData is null ? NotFound() : dictData;
    }
}
