using XW.Shared.WebApi.Routing;
using XW.System.Application.Contracts.Dtos.SysConfig;

namespace XW.System.API.Controllers;

/// <summary>
/// 系统配置管理
/// </summary>
[Route($"{ApiSurfaces.ConsoleRoutePrefix}/admin/sysconfigs")]
public class SysConfigController(ISysConfigService sysConfigService) : ConsoleApiController
{
    /// <summary>
    /// 创建系统配置
    /// </summary>
    /// <param name="input">创建参数</param>
    /// <returns>新记录主键</returns>
    [HttpPost]
    [AdncAuthorize(PermissionConsts.SysConfig.Create)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<IdDto>> CreateAsync([FromBody] SysConfigCreationDto input)
        => CreatedResult(await sysConfigService.CreateAsync(input));

    /// <summary>
    /// 更新系统配置
    /// </summary>
    /// <param name="id">主键</param>
    /// <param name="input">更新参数</param>
    /// <returns>操作结果</returns>
    [HttpPut("{id}")]
    [AdncAuthorize(PermissionConsts.SysConfig.Update)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<long>> UpdateAsync([FromRoute] long id, [FromBody] SysConfigUpdationDto input)
        => Result(await sysConfigService.UpdateAsync(id, input));

    /// <summary>
    /// 删除系统配置
    /// </summary>
    /// <param name="ids">主键列表</param>
    /// <returns>操作结果</returns>
    [HttpDelete("{ids}")]
    [AdncAuthorize(PermissionConsts.SysConfig.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteAsync([FromRoute] string ids)
    {
        var idArr = ids.Split(',').Select(long.Parse).ToArray();
        return Result(await sysConfigService.DeleteAsync(idArr));
    }

    /// <summary>
    /// 按主键获取系统配置
    /// </summary>
    /// <param name="id">主键</param>
    /// <returns>系统配置详情</returns>
    [HttpGet("{id}")]
    [AdncAuthorize([PermissionConsts.SysConfig.Get, PermissionConsts.SysConfig.Update])]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SysConfigDto>> GetAsync([FromRoute] long id)
    {
        var cfg = await sysConfigService.GetAsync(id);
        return cfg is null ? NotFound() : cfg;
    }

    /// <summary>
    /// 分页查询系统配置
    /// </summary>
    /// <param name="input">分页与关键字</param>
    /// <returns>分页结果</returns>
    [HttpGet("page")]
    [AdncAuthorize(PermissionConsts.SysConfig.Search)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PageModelDto<SysConfigDto>>> GetPagedAsync([FromQuery] SearchPagedDto input)
      => await sysConfigService.GetPagedAsync(input);

    /// <summary>
    /// 按配置键获取系统配置
    /// </summary>
    /// <param name="keys">配置键，传 all 获取全部</param>
    /// <returns>系统配置列表</returns>
    [HttpGet()]
    [AdncAuthorize(PermissionConsts.SysConfig.Search, AdncAuthorizeAttribute.JwtWithBasicSchemes)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SysConfigSimpleDto>>> GetListAsync([FromQuery] string keys)
        => await sysConfigService.GetListAsync(keys);
}
