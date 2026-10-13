using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using XW.Shared.WebApi.Routing;
using XW.System.Application.Contracts.Dtos.User;

namespace XW.System.API.Controllers;

/// <summary>
/// 账户会话管理
/// </summary>
[Route($"{ApiSurfaces.ConsoleRoutePrefix}/auth/session")]
public class AccountController(IOptions<JWTOptions> jwtOptions, UserContext userContext, IUserService userService, ILogger<AccountController> logger)
    : ConsoleApiController
{
    /// <summary>
    /// 用户登录
    /// </summary>
    /// <param name="input">登录参数</param>
    /// <returns>访问令牌与刷新令牌</returns>
    [AllowAnonymous]
    [HttpPost()]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<UserTokenInfoDto>> LoginAsync([FromBody] UserLoginDto input)
    {
        var result = await userService.LoginAsync(input);
        if (result.IsSuccess)
        {
            var validatedInfo = result.Content;
            var accessToken = JwtTokenHelper.CreateAccessToken(jwtOptions.Value, validatedInfo.ValidationVersion, validatedInfo.Account, validatedInfo.Id.ToString(), validatedInfo.Name, validatedInfo.GetRoleIdsString(), BearerDefaults.Manager);
            var refreshToken = JwtTokenHelper.CreateRefreshToken(jwtOptions.Value, validatedInfo.ValidationVersion, validatedInfo.Id.ToString());
            var tokenInfo = new UserTokenInfoDto(accessToken.Token, accessToken.Expire, refreshToken.Token, refreshToken.Expire);
            return Created($"{ApiSurfaces.ConsoleRoutePrefix}/auth/session", tokenInfo);
        }
        return Problem(result.ProblemDetails);
    }

    /// <summary>
    /// 用户登出
    /// </summary>
    /// <returns>操作结果</returns>
    [HttpDelete()]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> LogoutAsync()
        => Result(await userService.DeleteUserValidateInfoAsync(userContext.Id));

    /// <summary>
    /// 刷新访问令牌
    /// </summary>
    /// <param name="input">刷新令牌参数</param>
    /// <returns>新的访问令牌与刷新令牌</returns>
    [AllowAnonymous, HttpPut()]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserTokenInfoDto>> RefreshAccessTokenAsync([FromBody] UserRefreshTokenDto input)
    {
        var claimOfId = JwtTokenHelper.GetClaimFromRefeshToken(jwtOptions.Value, input.RefreshToken, JwtRegisteredClaimNames.NameId);
        if (claimOfId is not null)
        {
            var id = claimOfId.Value.ToLong();
            if (id is null)
            {
                return Forbid();
            }

            var validatedInfo = await userService.GetUserValidatedInfoAsync(id.Value);
            if (validatedInfo is null)
            {
                return Forbid();
            }

            var jti = JwtTokenHelper.GetClaimFromRefeshToken(jwtOptions.Value, input.RefreshToken, JwtRegisteredClaimNames.Jti);
            if (jti is null || jti.Value != validatedInfo.ValidationVersion)
            {
                return Forbid();
            }

            var accessToken = JwtTokenHelper.CreateAccessToken(jwtOptions.Value, validatedInfo.ValidationVersion, validatedInfo.Account, validatedInfo.Id.ToString(), validatedInfo.Name, validatedInfo.GetRoleIdsString(), BearerDefaults.Manager);
            var refreshToken = JwtTokenHelper.CreateRefreshToken(jwtOptions.Value, validatedInfo.ValidationVersion, validatedInfo.Id.ToString());

            await userService.ChangeUserValidateInfoExpiresDtAsync(id.Value);

            var tokenInfo = new UserTokenInfoDto(accessToken.Token, accessToken.Expire, refreshToken.Token, refreshToken.Expire);
            return Ok(tokenInfo);
        }
        return Forbid();
    }

    /// <summary>
    /// 修改当前用户密码
    /// </summary>
    /// <param name="input">修改密码参数</param>
    /// <returns>操作结果</returns>
    [HttpPatch("password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> ChangePassword([FromBody] UserProfileChangePwdDto input)
        => Result(await userService.UpdatePasswordAsync(userContext.Id, input));

    /// <summary>
    /// 获取当前用户验证信息
    /// </summary>
    /// <returns>用户验证信息</returns>
    [HttpGet()]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserValidatedInfoDto>> GetUserValidatedInfoAsync()
    {
        var validatedInfo = await userService.GetUserValidatedInfoAsync(userContext.Id);
        logger.LogDebug("UserContext:{Id}", userContext.Id);
        return validatedInfo is null ? NotFound() : validatedInfo;
    }

    /// <summary>
    /// 获取当前用户权限
    /// </summary>
    /// <param name="id">用户 Id</param>
    /// <param name="requestPermissions">待校验权限列表</param>
    /// <param name="userBelongsRoleIds">用户所属角色 Id（逗号分隔）</param>
    /// <returns>用户拥有的权限列表</returns>
    [HttpGet("{id}/permissions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<string>>> GetCurrenUserPermissions([FromRoute] long id, [FromQuery] IEnumerable<string> requestPermissions, [FromQuery] string userBelongsRoleIds)
    {
        if (id != userContext.Id)
        {
            var userContextId = userContext.Id;
            logger.LogDebug("id={id},usercontextid={userContextId}", id, userContextId);
            return Forbid();
        }
        var result = await userService.GetPermissionsAsync(id, requestPermissions, userBelongsRoleIds);
        return result ?? [];
    }

    /// <summary>
    /// 获取当前用户角色与权限信息
    /// </summary>
    /// <returns>用户角色与权限信息</returns>
    [HttpGet("userinfo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserInfoDto>> GetUserInfoAsync()
    {
        var userInfo = await userService.GetUserInfoAsync(userContext);
        return userInfo is null ? NotFound() : userInfo;
    }

    /// <summary>
    /// 获取当前用户资料
    /// </summary>
    /// <returns>用户资料</returns>
    [HttpGet("profile")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileDto>> GetUserProfileAsync()
    {
        var profile = await userService.GetProfileAsync(userContext.Id);
        return profile is null ? NotFound() : profile;
    }

    /// <summary>
    /// 更新当前用户资料
    /// </summary>
    /// <param name="input">更新参数</param>
    /// <returns>操作结果</returns>
    [HttpPatch("profile")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> ChangeProfileAsync([FromBody] UserProfileUpdationDto input)
        => Result(await userService.ChangeProfileAsync(userContext.Id, input));
}
