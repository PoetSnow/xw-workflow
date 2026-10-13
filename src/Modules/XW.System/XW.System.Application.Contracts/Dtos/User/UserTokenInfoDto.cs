namespace XW.System.Application.Contracts.Dtos.User;

/// <summary>
/// 用户令牌信息
/// </summary>
public record UserTokenInfoDto : IDto
{
    /// <summary>
    /// 初始化用户令牌信息
    /// </summary>
    /// <param name="token">访问令牌</param>
    /// <param name="exprie">访问令牌过期时间</param>
    /// <param name="refreshToken">刷新令牌</param>
    /// <param name="refreshExprie">刷新令牌过期时间</param>
    public UserTokenInfoDto(string token, DateTime exprie, string refreshToken, DateTime refreshExprie)
    {
        Token = token;
        Expire = exprie;
        RefreshToken = refreshToken;
        RefreshExpire = refreshExprie;
    }

    /// <summary>访问令牌</summary>
    public string Token { get; set; }

    /// <summary>访问令牌过期时间</summary>
    public DateTime Expire { get; set; }

    /// <summary>刷新令牌</summary>
    public string RefreshToken { get; set; }

    /// <summary>刷新令牌过期时间</summary>
    public DateTime RefreshExpire { get; set; }
}
