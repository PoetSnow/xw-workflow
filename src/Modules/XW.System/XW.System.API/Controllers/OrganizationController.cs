using XW.Shared.WebApi.Routing;
using XW.System.Application.Contracts.Dtos.Organization;

namespace XW.System.API.Controllers;

/// <summary>
/// 组织管理
/// </summary>
[Route($"{ApiSurfaces.ConsoleRoutePrefix}/admin/organizations")]
public class OrganizationController(IOrganizationService organizationService) : ConsoleApiController
{
    /// <summary>
    /// 创建组织
    /// </summary>
    /// <param name="input">创建参数</param>
    /// <returns>新记录主键</returns>
    [HttpPost]
    [AdncAuthorize(PermissionConsts.Org.Create)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<IdDto>> CreateAsync([FromBody] OrganizationCreationDto input)
        => CreatedResult(await organizationService.CreateAsync(input));

    /// <summary>
    /// 更新组织
    /// </summary>
    /// <param name="id">主键</param>
    /// <param name="input">更新参数</param>
    /// <returns>操作结果</returns>
    [HttpPut("{id}")]
    [AdncAuthorize(PermissionConsts.Org.Update)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<long>> UpdateAsync([FromRoute] long id, [FromBody] OrganizationUpdationDto input)
        => Result(await organizationService.UpdateAsync(id, input));

    /// <summary>
    /// 删除组织
    /// </summary>
    /// <param name="ids">主键列表</param>
    /// <returns>操作结果</returns>
    [HttpDelete("{ids}")]
    [AdncAuthorize(PermissionConsts.Org.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete([FromRoute] string ids)
    {
        var idArr = ids.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(long.Parse).ToArray();
        return Result(await organizationService.DeleteAsync(idArr));
    }

    /// <summary>
    /// 按主键获取组织
    /// </summary>
    /// <param name="id">主键</param>
    /// <returns>组织详情</returns>
    [HttpGet("{id}")]
    [AdncAuthorize([PermissionConsts.Org.Get, PermissionConsts.Org.Update])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationDto>> GetAsync([FromRoute] long id)
    {
        var org = await organizationService.GetAsync(id);
        return org is null ? NotFound() : org;
    }

    /// <summary>
    /// 获取组织树
    /// </summary>
    /// <param name="keywords">组织名称关键字</param>
    /// <param name="status">启用状态</param>
    /// <returns>组织树</returns>
    [HttpGet()]
    [AdncAuthorize(PermissionConsts.Org.Search, AdncAuthorizeAttribute.JwtWithBasicSchemes)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OrganizationTreeDto>>> GetTreeListAsync(string? keywords = null, bool? status = null)
        => await organizationService.GetTreeListAsync(keywords, status);

    /// <summary>
    /// 获取组织选项
    /// </summary>
    /// <returns>组织选项树</returns>
    [HttpGet("options")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OptionTreeDto>>> GetOrgOptionsAsync()
        => await organizationService.GetOrgOptionsAsync(true);
}
