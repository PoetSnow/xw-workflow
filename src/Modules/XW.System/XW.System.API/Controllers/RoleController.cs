using XW.Shared.WebApi.Routing;
using XW.System.Application.Contracts.Dtos.Role;

namespace XW.System.API.Controllers;

/// <summary>
/// 角色管理
/// </summary>
[Route($"{ApiSurfaces.ConsoleRoutePrefix}/admin/roles")]
public class RoleController(IRoleService roleService) : ConsoleApiController
{
    /// <summary>
    /// 创建角色
    /// </summary>
    /// <param name="input">创建参数</param>
    /// <returns>新记录主键</returns>
    [HttpPost]
    [AdncAuthorize(PermissionConsts.Role.Create)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<IdDto>> CreateAsync([FromBody] RoleCreationDto input)
        => CreatedResult(await roleService.CreateAsync(input));

    /// <summary>
    /// 更新角色
    /// </summary>
    /// <param name="id">主键</param>
    /// <param name="input">更新参数</param>
    /// <returns>操作结果</returns>
    [HttpPut("{id}")]
    [AdncAuthorize(PermissionConsts.Role.Update)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> UpdateAsync([FromRoute] long id, [FromBody] RoleUpdationDto input)
        => Result(await roleService.UpdateAsync(id, input));

    /// <summary>
    /// 删除角色
    /// </summary>
    /// <param name="ids">主键列表</param>
    /// <returns>操作结果</returns>
    [HttpDelete("{ids}")]
    [AdncAuthorize(PermissionConsts.Role.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteAsync([FromRoute] string ids)
    {
        var idArr = ids.Split(",").Select(long.Parse).ToArray();
        return Result(await roleService.DeleteAsync(idArr));
    }

    /// <summary>
    /// 分页查询角色
    /// </summary>
    /// <param name="input">分页与关键字</param>
    /// <returns>分页结果</returns>
    [HttpGet("page")]
    [AdncAuthorize(PermissionConsts.Role.Search)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PageModelDto<RoleDto>>> GetPagedAsync([FromQuery] SearchPagedDto input)
        => await roleService.GetPagedAsync(input);

    /// <summary>
    /// 按主键获取角色
    /// </summary>
    /// <param name="id">主键</param>
    /// <returns>角色详情</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [AdncAuthorize([PermissionConsts.Role.Get, PermissionConsts.Role.Update])]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoleDto>> GetAsync([FromRoute] long id)
    {
        var role = await roleService.GetAsync(id);
        return role is null ? NotFound() : role;
    }

    /// <summary>
    /// 保存角色菜单权限
    /// </summary>
    /// <param name="id">角色 Id</param>
    /// <param name="permissions">菜单权限 Id 列表</param>
    /// <returns>操作结果</returns>
    [HttpPatch("{id}/permissons")]
    [AdncAuthorize(PermissionConsts.Role.SetPermissons)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> SetPermissonsAsync([FromRoute] long id, [FromBody] long[] permissions)
        => Result(await roleService.SetPermissonsAsync(new RoleSetPermissonsDto() { RoleId = id, Permissions = permissions }));

    /// <summary>
    /// 获取角色已分配菜单 Id
    /// </summary>
    /// <param name="id">角色 Id</param>
    /// <returns>菜单 Id 列表</returns>
    [HttpGet("{id}/menuids")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<long[]>> GetMenuIdsAsync([FromRoute] long id)
        => await roleService.GetMenuIdsAsync(id);

    /// <summary>
    /// 获取角色选项
    /// </summary>
    /// <returns>角色选项列表</returns>
    [HttpGet("options")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OptionTreeDto>>> GetOptionsAsync()
        => await roleService.GetOptionsAsync();
}
