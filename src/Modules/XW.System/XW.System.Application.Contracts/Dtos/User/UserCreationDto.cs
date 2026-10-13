namespace XW.System.Application.Contracts.Dtos.User;

/// <summary>
/// 用户创建参数
/// </summary>
public class UserCreationDto : UserCreationAndUpdationDto
{
    /// <summary>账号</summary>
    public string Account { get; set; } = string.Empty;
}
