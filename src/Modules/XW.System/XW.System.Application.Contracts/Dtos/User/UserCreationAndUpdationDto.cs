namespace XW.System.Application.Contracts.Dtos.User;

/// <summary>
/// 用户创建与更新公共字段
/// </summary>
public abstract class UserCreationAndUpdationDto : InputDto
{
    /// <summary>生日</summary>
    public DateTime? Birthday { get; set; }

    /// <summary>部门 Id</summary>
    public long DeptId { get; set; }

    /// <summary>邮箱</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>姓名</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>手机号</summary>
    public string Mobile { get; set; } = string.Empty;

    /// <summary>角色 Id 列表</summary>
    public long[] RoleIds { get; set; } = [];

    /// <summary>任职。空则按 DeptId 自动写一份主职员工岗。</summary>
    public List<UserAppointmentDto> Appointments { get; set; } = [];

    /// <summary>性别</summary>
    public int Gender { get; set; }

    /// <summary>启用状态</summary>
    public bool Status { get; set; }
}
