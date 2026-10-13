using XW.Shared.WebApi.Routing;
using XW.System.Application.Contracts.Dtos.Dict;

namespace XW.System.API.Controllers
{
    /// <summary>
    /// 字典管理
    /// </summary>
    [Route($"{ApiSurfaces.ConsoleRoutePrefix}/admin/dicts")]
    public class DictController(IDictService dictService) : ConsoleApiController
    {

        /// <summary>
        /// 创建字典
        /// </summary>
        /// <param name="input">创建参数</param>
        /// <returns>新记录主键</returns>
        [HttpPost]
        [AdncAuthorize(PermissionConsts.Dict.Create)]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public async Task<ActionResult<IdDto>> CreateAsync([FromBody] DictCreationDto input)
            => CreatedResult(await dictService.CreateAsync(input));

        /// <summary>
        /// 更新字典
        /// </summary>
        /// <param name="id">主键</param>
        /// <param name="input">更新参数</param>
        /// <returns>操作结果</returns>
        [HttpPut("{id}")]
        [AdncAuthorize(PermissionConsts.Dict.Update)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<ActionResult<long>> UpdateAsync([FromRoute] long id, [FromBody] DictUpdationDto input)
            => Result(await dictService.UpdateAsync(id, input));

        /// <summary>
        /// 删除字典
        /// </summary>
        /// <param name="ids">主键列表</param>
        /// <returns>操作结果</returns>
        [HttpDelete("{ids}")]
        [AdncAuthorize(PermissionConsts.Dict.Delete)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<ActionResult> DeleteAsync([FromRoute] string ids)
        {
            var idArr = ids.Split(',').Select(long.Parse).ToArray();
            return Result(await dictService.DeleteAsync(idArr));
        }

        /// <summary>
        /// 分页查询字典
        /// </summary>
        /// <param name="input">分页与关键字</param>
        /// <returns>分页结果</returns>
        [HttpGet("page")]
        [AdncAuthorize(PermissionConsts.Dict.Search)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<PageModelDto<DictDto>>> GetPagedAsync([FromQuery] DictDataSearchPagedDto input)
            => await dictService.GetPagedAsync(input);

        /// <summary>
        /// 按主键获取字典
        /// </summary>
        /// <param name="id">主键</param>
        /// <returns>字典详情</returns>
        [HttpGet("{id}")]
        [AdncAuthorize([PermissionConsts.Dict.Get, PermissionConsts.Dict.Update])]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<DictDto>> GetAsync([FromRoute] long id)
        {
            var dict = await dictService.GetAsync(id);
            return dict is null ? NotFound() : dict;
        }

        /// <summary>
        /// 按编码获取字典选项
        /// </summary>
        /// <param name="codes">字典编码</param>
        /// <returns>字典选项列表</returns>
        [HttpGet("options")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<List<DictOptionDto>>> GetOptionsAsync([FromQuery] string codes)
            => await dictService.GetOptionsAsync(codes);
    }
}
