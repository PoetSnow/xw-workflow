namespace XW.System.Application.Contracts.Dtos.User;

/// <summary>
/// 刷新令牌参数
/// </summary>
public class UserRefreshTokenDto : InputDto
{
    /// <summary>刷新令牌</summary>
    public string RefreshToken { get; set; } = string.Empty;
}
