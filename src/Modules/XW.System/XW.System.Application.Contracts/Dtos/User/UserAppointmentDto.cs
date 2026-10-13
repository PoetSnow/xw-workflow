namespace XW.System.Application.Contracts.Dtos.User;

/// <summary>用户任职</summary>
[Serializable]
public class UserAppointmentDto
{
    /// <summary>主键</summary>
    public long Id { get; set; }

    /// <summary>用户 Id</summary>
    public long UserId { get; set; }

    /// <summary>部门 Id</summary>
    public long DeptId { get; set; }

    /// <summary>部门名称</summary>
    public string DeptName { get; set; } = string.Empty;

    /// <summary>岗位 Id</summary>
    public long PositionId { get; set; }

    /// <summary>岗位编码</summary>
    public string PositionCode { get; set; } = string.Empty;

    /// <summary>岗位名称</summary>
    public string PositionName { get; set; } = string.Empty;

    /// <summary>是否主职</summary>
    public bool IsPrimary { get; set; }

    /// <summary>是否有效</summary>
    public bool Status { get; set; } = true;

    /// <summary>生效开始</summary>
    public DateTime? EffectiveStart { get; set; }

    /// <summary>生效结束</summary>
    public DateTime? EffectiveEnd { get; set; }
}
