namespace XW.System.Application.Contracts.Dtos.User;

/// <summary>
/// 用户登录参数
/// </summary>
public class UserLoginDto : InputDto
{
    /// <summary>账号</summary>
    public string Account { get; set; } = string.Empty;

    /// <summary>密码</summary>
    public string Password { get; set; } = string.Empty;
}
