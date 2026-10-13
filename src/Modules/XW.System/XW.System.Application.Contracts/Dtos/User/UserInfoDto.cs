namespace XW.System.Application.Contracts.Dtos.User;

/// <summary>
/// 当前用户角色与权限信息
/// </summary>
public class UserInfoDto : OutputDto
{
    private string _avatar = string.Empty;

    /// <summary>账号</summary>
    public string Account { get; set; } = string.Empty;

    /// <summary>姓名</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>头像地址</summary>
    public string Avatar
    {
        set { _avatar = value; }
        get
        {
            if (_avatar.IsNullOrEmpty())
            {
                _avatar = "https://foruda.gitee.com/images/1723603502796844527/03cdca2a_716974.gif";
            }
            return _avatar;
        }
    }

    /// <summary>角色编码列表</summary>
    public string[] Roles { get; set; } = [];

    /// <summary>权限编码列表</summary>
    public string[] Perms { get; set; } = [];
}
