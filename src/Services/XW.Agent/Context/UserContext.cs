namespace XW.Agent.Application.Context;

/// <summary>一次请求的用户/租户上下文（贯穿 Runtime / Policy / Integration / Audit）。</summary>
public sealed class UserContext
{
    /// <summary>用户 Id。</summary>
    public string? UserId { get; init; }

    /// <summary>租户 Id。</summary>
    public string? TenantId { get; init; }

    /// <summary>展示名（可选）。</summary>
    public string? DisplayName { get; init; }

    /// <summary>角色列表。</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>显式权限码。</summary>
    public IReadOnlyList<string> PermissionCodes { get; init; } = [];

    /// <summary>入口带来的调用方 Token（forward_caller 下游鉴权用）。</summary>
    public string? AccessToken { get; init; }

    /// <summary>渠道（web / api 等）。</summary>
    public string Channel { get; init; } = "web";

    /// <summary>高风险操作是否已确认。</summary>
    public bool Confirmed { get; init; }

    /// <summary>本轮 TraceId。</summary>
    public string TraceId { get; init; } = string.Empty;
}

/// <summary>请求范围内的 UserContext 访问器（AsyncLocal）。</summary>
public interface IUserContextAccessor
{
    /// <summary>当前请求上下文；请求结束应置空。</summary>
    UserContext? Current { get; set; }
}

/// <summary>基于 AsyncLocal 的 UserContext 访问器实现。</summary>
public sealed class AsyncLocalUserContextAccessor : IUserContextAccessor
{
    private static readonly AsyncLocal<UserContext?> Holder = new();

    /// <inheritdoc />
    public UserContext? Current
    {
        get => Holder.Value;
        set => Holder.Value = value;
    }
}
