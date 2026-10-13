using XW.Agent.Application.Context;
using XW.Agent.Application.Options;

namespace XW.Agent.Application.Policy;

/// <summary>
/// 按业务接口划分调用面。
/// 运营后台持有菜单码 pot-cooking 时只能查看。
/// 小程序会员没有该菜单码，只能走自己的任务接口；是否属于本人由 Host 按登录用户判断。
/// </summary>
public static class AgentSurfacePolicy
{
    public const string Console = "console";
    public const string Portal = "portal";

    /// <summary>后台「制作任务」菜单权限码，与 sys_menu.perm 一致。</summary>
    public const string PotCookingMenu = "pot-cooking";

    public static bool Allow(AgentCapabilityDefinition capability, UserContext user, IPermissionPolicy policy)
    {
        var surface = capability.Surface?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(surface))
        {
            return true;
        }

        var consoleViewer = IsConsoleViewer(policy.ResolveEffectivePermissions(user));
        if (surface == Console)
        {
            return consoleViewer;
        }

        if (surface == Portal)
        {
            // 后台身份只查看，不改走小程序的启动/取消。
            if (consoleViewer || IsStaff(policy.ResolveEffectivePermissions(user)))
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(user.AccessToken)
                   || !string.IsNullOrWhiteSpace(user.UserId);
        }

        return true;
    }

    private static bool IsConsoleViewer(IReadOnlyList<string> effective) =>
        effective.Any(p =>
            p == "*"
            || string.Equals(p, PotCookingMenu, StringComparison.OrdinalIgnoreCase));

    /// <summary>已展开出后台能力码或壶菜单码的调用方，不是小程序会员。</summary>
    private static bool IsStaff(IReadOnlyList<string> effective) =>
        effective.Any(p =>
            p == "*"
            || p.StartsWith("agent.", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("pot-", StringComparison.OrdinalIgnoreCase));
}
