using XW.Shared.WebApi.Routing;
using XW.System.Application.Contracts.Dtos.Menu;

namespace XW.System.API.Controllers
{

    /// <summary>
    /// 菜单管理
    /// </summary>
    [Route($"{ApiSurfaces.ConsoleRoutePrefix}/admin/menus")]
    public class MenuController(IMenuService menuService, UserContext userContext) : ConsoleApiController
    {


        /// <summary>
        /// 获取菜单树
        /// </summary>
        /// <param name="keywords">关键字</param>
        /// <returns>菜单树</returns>
        [HttpGet()]
        //[AdncAuthorize(PermissionConsts.Menu.Search)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<List<MenuTreeDto>>> GetTreelistAsync(string? keywords = null)
            => await menuService.GetTreelistAsync(keywords);

        /// <summary>
        /// 获取当前用户侧边栏路由菜单
        /// </summary>
        /// <returns>路由菜单树</returns>
        [HttpGet("routers")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<List<RouterTreeDto>>> GetMenusForRouterAsync()
        {
            var roleIds = userContext.RoleIds.Split(",", StringSplitOptions.RemoveEmptyEntries) ?? [];
            return await menuService.GetMenusForRouterAsync(roleIds.Select(long.Parse));
        }
        /// <summary>
        /// 获取菜单选项
        /// </summary>
        /// <param name="onlyParent">是否仅返回可作为父级的节点</param>
        /// <returns>菜单选项树</returns>
        [HttpGet("options")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<List<OptionTreeDto>>> GetMenuOptionsAsync(bool? onlyParent)
        {
            return await menuService.GetMenuOptionsAsync(onlyParent);
        }

        /// <summary>
        /// 按主键获取菜单
        /// </summary>
        /// <param name="id">主键</param>
        /// <returns>菜单详情</returns>
        [HttpGet("{id}")]
        [AdncAuthorize([PermissionConsts.Menu.Get, PermissionConsts.Menu.Update])]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MenuDto>> GetAsync([FromRoute] long id)
        {
            var menu = await menuService.GetAsync(id);
            return menu is null ? NotFound() : menu;
        }

        /// <summary>
        /// 创建菜单
        /// </summary>
        /// <param name="input">创建参数</param>
        /// <returns>新记录主键</returns>
        [HttpPost]
        [AdncAuthorize(PermissionConsts.Menu.Create)]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public async Task<ActionResult<IdDto>> CreateAsync([FromBody] MenuCreationDto input)
            => CreatedResult(await menuService.CreateAsync(input));

        /// <summary>
        /// 更新菜单
        /// </summary>
        /// <param name="id">主键</param>
        /// <param name="input">更新参数</param>
        /// <returns>操作结果</returns>
        [HttpPut("{id}")]
        [AdncAuthorize(PermissionConsts.Menu.Update)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<ActionResult> UpdateAsync([FromRoute] long id, [FromBody] MenuUpdationDto input)
            => Result(await menuService.UpdateAsync(id, input));

        /// <summary>
        /// 删除菜单
        /// </summary>
        /// <param name="id">主键</param>
        /// <returns>操作结果</returns>
        [HttpDelete("{id}")]
        [AdncAuthorize(PermissionConsts.Menu.Delete)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<ActionResult> DeleteAsync([FromRoute] long id)
            => Result(await menuService.DeleteAsync(id));
    }
}
