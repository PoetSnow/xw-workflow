using Microsoft.Extensions.Options;
using XW.Agent.Application.Context;
using XW.Agent.Application.Options;

namespace XW.Agent.Application.Policy;

/// <summary>能力权限策略：判断用户是否允许调用指定权限码。</summary>
public interface IPermissionPolicy
{
    /// <summary>单权限校验。</summary>
    bool IsAllowed(UserContext user, string requiredPermission);

    /// <summary>Capability 多权限：全部满足才允许（AND）。空列表视为无门槛。</summary>
    bool IsAllowedAll(UserContext user, IEnumerable<string> requiredPermissions);

    /// <summary>合并用户 PermissionCodes 与角色映射后的有效权限集。</summary>
    IReadOnlyList<string> ResolveEffectivePermissions(UserContext user);
}

/// <summary>高风险操作确认闸门（ConfirmRequired）。</summary>
public interface IApprovalGate
{
    /// <summary>根据能力是否要求确认与用户 Confirmed 标记做出放行/拦截。</summary>
    ApprovalDecision Evaluate(bool confirmRequired, UserContext user);
}

/// <summary>确认闸门结果。</summary>
/// <param name="Allowed">是否允许继续执行。</param>
/// <param name="Message">拦截时给用户的说明。</param>
public sealed record ApprovalDecision(bool Allowed, string? Message);

/// <summary>
/// 可配置权限策略：角色→权限映射 + DenyByDefault；
/// 开发态无任何权限声明时可放行便于联调。
/// </summary>
public sealed class ConfigurablePermissionPolicy(IOptionsMonitor<AgentOptions> options) : IPermissionPolicy
{
    /// <inheritdoc />
    public IReadOnlyList<string> ResolveEffectivePermissions(UserContext user)
    {
        var set = new HashSet<string>(user.PermissionCodes, StringComparer.OrdinalIgnoreCase);
        var policy = options.CurrentValue.Policy;
        var map = policy.RolePermissions;
        foreach (var role in user.Roles)
        {
            if (map.TryGetValue(role, out var perms))
            {
                foreach (var p in perms)
                {
                    set.Add(p);
                }
            }
        }

        // Console 传的是菜单权限码，展开为 Agent 能力权限码
        var aliases = policy.PermissionAliases;
        if (aliases.Count > 0)
        {
            foreach (var code in set.ToList())
            {
                if (!aliases.TryGetValue(code, out var agentPerms))
                {
                    continue;
                }

                foreach (var p in agentPerms)
                {
                    set.Add(p);
                }
            }
        }

        return set.ToList();
    }

    /// <inheritdoc />
    public bool IsAllowedAll(UserContext user, IEnumerable<string> requiredPermissions)
    {
        var required = requiredPermissions
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (required.Count == 0)
        {
            return true;
        }

        return required.All(p => IsAllowed(user, p));
    }

    /// <inheritdoc />
    public bool IsAllowed(UserContext user, string requiredPermission)
    {
        if (options.CurrentValue.Policy.Bypass)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(requiredPermission))
        {
            return true;
        }

        var effective = ResolveEffectivePermissions(user);
        if (effective.Any(p => p == "*" || string.Equals(p, requiredPermission, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // 兼容：请求显式带了 permissionCodes
        if (user.PermissionCodes.Any(p =>
                p == "*" || string.Equals(p, requiredPermission, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (options.CurrentValue.Policy.DenyByDefault)
        {
            return false;
        }

        // 开发态：无任何权限声明时放行，便于联调
        return effective.Count == 0 && user.PermissionCodes.Count == 0 && user.Roles.Count == 0;
    }
}

/// <summary>基于 UserContext.Confirmed 的确认闸门实现。</summary>
public sealed class ConfirmApprovalGate : IApprovalGate
{
    /// <inheritdoc />
    public ApprovalDecision Evaluate(bool confirmRequired, UserContext user)
    {
        if (!confirmRequired)
        {
            return new ApprovalDecision(true, null);
        }

        if (user.Confirmed)
        {
            return new ApprovalDecision(true, null);
        }

        return new ApprovalDecision(
            false,
            "该操作需要确认。请在确认后重试，并设置 confirmed=true。");
    }
}
