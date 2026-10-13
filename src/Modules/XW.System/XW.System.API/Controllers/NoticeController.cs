using XW.Shared.WebApi.Routing;
using XW.System.Application.Contracts.Dtos.Notice;

namespace XW.System.API.Controllers;

/// <summary>
/// 通知公告管理
/// </summary>
[Route($"{ApiSurfaces.ConsoleRoutePrefix}/admin/notices")]
public class NoticeController() : ConsoleApiController
{
    /*
    /// <summary>
    /// 创建通知公告
    /// </summary>
    /// <param name="input">创建参数</param>
    /// <returns>新记录主键</returns>
    //[HttpPost]
    //[AdncAuthorize(PermissionConsts.SysConfig.Create)]
    //[ProducesResponseType(StatusCodes.Status201Created)]
    //public async Task<ActionResult<long>> CreateAsync([FromBody] SysConfigCreationDto input)
    //    =>  CreatedResult(await sysConfigService.CreateAsync(input));

    /// <summary>
    /// 更新通知公告
    /// </summary>
    /// <param name="id">主键</param>
    /// <param name="input">更新参数</param>
    /// <returns>操作结果</returns>
    //[HttpPut("{id}")]
    //[AdncAuthorize(PermissionConsts.SysConfig.Update)]
    //[ProducesResponseType(StatusCodes.Status204NoContent)]
    //public async Task<ActionResult<long>> UpdateAsync([FromRoute] long id, [FromBody] SysConfigUpdationDto input)
    //    => Result(await sysConfigService.UpdateAsync(id, input));

    /// <summary>
    /// 删除通知公告
    /// </summary>
    /// <param name="ids">主键列表</param>
    /// <returns>操作结果</returns>
    //[HttpDelete("{ids}")]
    //[AdncAuthorize(PermissionConsts.SysConfig.Delete)]
    //[ProducesResponseType(StatusCodes.Status204NoContent)]
    //public async Task<ActionResult> DeleteAsync([FromRoute] string ids)
    //{
    //    var idArr = ids.Split(',').Select(x => long.Parse(x)).ToArray();
    //    return Result(await sysConfigService.DeleteAsync(idArr));
    //}

    /// <summary>
    /// 按主键获取通知公告
    /// </summary>
    /// <param name="id">主键</param>
    /// <returns>通知公告详情</returns>
    //[HttpGet("{id}")]
    //// [AdncAuthorize(PermissionConsts.SysConfig.Search, AdncAuthorizeAttribute.JwtWithBasicSchemes)]
    //[ProducesResponseType(StatusCodes.Status200OK)]
    //[ProducesResponseType(StatusCodes.Status404NotFound)]
    //public async Task<ActionResult<SysConfigDto>> GetAsync([FromRoute] long id)
    //{
    //    var cfg = await sysConfigService.GetAsync(id);
    //    return cfg is null ? NotFound() : cfg;
    //}
    */

    /// <summary>
    /// 分页查询当前用户通知公告
    /// </summary>
    /// <param name="input">分页与关键字</param>
    /// <returns>分页结果</returns>
    [HttpGet("mine")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PageModelDto<NoticeDto>>> GetMinePagedAsync([FromQuery] NoticeSearchPagedDto input)
    {
        await Task.CompletedTask;
        return new PageModelDto<NoticeDto>(input);
    }
}
