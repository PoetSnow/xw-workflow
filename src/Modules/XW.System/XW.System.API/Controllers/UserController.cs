using XW.Shared.WebApi.Routing;
using XW.System.Application.Contracts.Dtos.User;

namespace XW.System.API.Controllers;

/// <summary>
/// 用户管理
/// </summary>
[Route($"{ApiSurfaces.ConsoleRoutePrefix}/admin/users")]
public class UserController(IUserService userService, UserContext userContext) : ConsoleApiController
{
    /// <summary>
    /// 创建用户
    /// </summary>
    /// <param name="input">创建参数</param>
    /// <returns>新记录主键</returns>
    [HttpPost]
    [AdncAuthorize(PermissionConsts.User.Create)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<IdDto>> CreateAsync([FromBody] UserCreationDto input)
        => CreatedResult(await userService.CreateAsync(input));

    /// <summary>
    /// 更新用户
    /// </summary>
    /// <param name="id">主键</param>
    /// <param name="input">更新参数</param>
    /// <returns>操作结果</returns>
    [HttpPut("{id}")]
    [AdncAuthorize(PermissionConsts.User.Update)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> UpdateAsync([FromRoute] long id, [FromBody] UserUpdationDto input)
        => Result(await userService.UpdateAsync(id, input));

    /// <summary>
    /// 删除用户
    /// </summary>
    /// <param name="ids">主键列表</param>
    /// <returns>操作结果</returns>
    [HttpDelete("{ids}")]
    [AdncAuthorize(PermissionConsts.User.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteAsync([FromRoute] string ids)
    {
        var idArr = ids.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(long.Parse).ToArray();
        return Result(await userService.DeleteAsync(idArr));
    }

    /// <summary>
    /// 重置用户密码
    /// </summary>
    /// <param name="id">主键</param>
    /// <param name="password">新密码</param>
    /// <returns>操作结果</returns>
    [HttpPatch("{id}/password")]
    [AdncAuthorize(PermissionConsts.User.ResetPassword)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> ResetPasswordAsync([FromRoute] long id, string password)
        => Result(await userService.ResetPasswordAsync(id, password));

    /// <summary>
    /// 按主键获取用户
    /// </summary>
    /// <param name="id">主键</param>
    /// <returns>用户详情</returns>
    [HttpGet("{id:long}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [AdncAuthorize([PermissionConsts.User.Get, PermissionConsts.User.Update])]
    public async Task<ActionResult<UserDto>> GetAsync([FromRoute] long id)
    {
        var user = await userService.GetAsync(id);
        return user is null ? NotFound() : user;
    }

    /// <summary>当前登录用户的有效任职，提交单据选部门用。</summary>
    [HttpGet("me/appointments")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UserAppointmentDto>>> GetMyAppointmentsAsync(
        [FromServices] IUserAppointmentService appointmentService)
        => await appointmentService.GetActiveByUserAsync(userContext.Id);

    /// <summary>某用户的全部任职。</summary>
    [HttpGet("{id:long}/appointments")]
    [AdncAuthorize([PermissionConsts.User.Get, PermissionConsts.User.Update])]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UserAppointmentDto>>> GetAppointmentsAsync(
        [FromRoute] long id,
        [FromServices] IUserAppointmentService appointmentService)
        => await appointmentService.GetByUserAsync(id);

    /// <summary>整表替换某用户任职。</summary>
    [HttpPut("{id:long}/appointments")]
    [AdncAuthorize(PermissionConsts.User.Update)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> ReplaceAppointmentsAsync(
        [FromRoute] long id,
        [FromBody] List<UserAppointmentDto> input,
        [FromServices] IUserAppointmentService appointmentService)
        => Result(await appointmentService.ReplaceByUserAsync(id, input));

    /// <summary>
    /// 分页查询用户
    /// </summary>
    /// <param name="input">分页与关键字</param>
    /// <returns>分页结果</returns>
    [HttpGet("page")]
    [AdncAuthorize(PermissionConsts.User.Search)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PageModelDto<UserDto>>> GetPagedAsync([FromQuery] UserSearchPagedDto input)
        => await userService.GetPagedAsync(input);
}
