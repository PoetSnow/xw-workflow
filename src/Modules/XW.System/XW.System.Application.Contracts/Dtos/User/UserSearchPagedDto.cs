namespace XW.System.Application.Contracts.Dtos.User;

/// <summary>
/// 用户分页查询参数
/// </summary>
public class UserSearchPagedDto : SearchPagedDto
{
    /// <summary>启用状态</summary>
    public bool? Status { get; set; }

    /// <summary>部门 Id</summary>
    public long? DeptId { get; set; }
}
