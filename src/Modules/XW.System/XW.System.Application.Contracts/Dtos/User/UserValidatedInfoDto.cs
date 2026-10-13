namespace XW.System.Application.Contracts.Dtos.User;

/// <summary>
/// 用户会话验证信息
/// </summary>
[Serializable]
public record UserValidatedInfoDto : IDto
{
    /// <summary>
    /// 初始化用户会话验证信息
    /// </summary>
    /// <param name="id">用户 Id</param>
    /// <param name="account">账号</param>
    /// <param name="name">姓名</param>
    /// <param name="roleids">角色 Id 列表</param>
    /// <param name="roleCodes">角色编码列表</param>
    /// <param name="roleNames">角色名称列表</param>
    /// <param name="status">启用状态</param>
    public UserValidatedInfoDto(long id, string account, string name, long[] roleids, string[] roleCodes, string[] roleNames, bool status)
    {
        Id = id;
        Account = account;
        Name = name;
        RoleIds = roleids;
        RoleCodes = roleCodes;
        RoleNames = roleNames;
        Status = status;
        ValidationVersion = Guid.NewGuid().ToString("N");
    }

    /// <summary>主键</summary>
    public long Id { get; init; }

    /// <summary>账号</summary>
    public string Account { get; init; }

    /// <summary>姓名</summary>
    public string Name { get; init; }

    /// <summary>角色 Id 列表</summary>
    public long[] RoleIds { get; init; }

    /// <summary>角色编码列表</summary>
    public string[] RoleCodes { get; init; }

    /// <summary>角色名称列表</summary>
    public string[] RoleNames { get; init; }

    /// <summary>启用状态</summary>
    public bool Status { get; init; }

    /// <summary>验证版本号</summary>
    public string ValidationVersion { get; init; }

    /// <summary>
    /// 获取逗号分隔的角色 Id 字符串
    /// </summary>
    /// <returns>角色 Id 字符串</returns>
    public string GetRoleIdsString() => string.Join(',', RoleIds);
}
