namespace XW.System.Application.Contracts.Dtos.User;

/// <summary>
/// 用户详情
/// </summary>
[Serializable]
public class UserDto : UserCreationAndUpdationDto
{
    /// <summary>主键</summary>
    public long Id { get; set; }

    /// <summary>账号</summary>
    public string Account { get; set; } = string.Empty;

    /// <summary>部门名称</summary>
    public string DeptName { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }
}
