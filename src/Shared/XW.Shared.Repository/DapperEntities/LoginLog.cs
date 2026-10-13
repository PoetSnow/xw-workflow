using XW.Infra.Repository;

namespace XW.Shared.Repository.DapperEntities;

/// <summary>登录日志（写入 SysLogDb · login_log）。</summary>
public class LoginLog : Entity
{
    public string Device { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public bool Succeed { get; set; }

    public int StatusCode { get; set; }

    public long UserId { get; set; }

    public string Account { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string RemoteIpAddress { get; set; } = string.Empty;

    public int ExecutionTime { get; set; }

    public DateTime CreateTime { get; set; }
}
