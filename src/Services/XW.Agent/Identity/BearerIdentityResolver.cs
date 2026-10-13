using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using XW.Agent.Application.Context;

namespace XW.Agent.Application.Identity;

/// <summary>从 HTTP / 请求体解析调用方身份（对接企业 SSO 的薄适配，不自建用户库）。</summary>
public interface IIdentityResolver
{
    /// <summary>
    /// 合并 Authorization Bearer、请求体显式字段，产出 <see cref="UserContext"/>。
    /// </summary>
    UserContext Resolve(
        HttpContext? http,
        string? explicitUserId,
        string? explicitTenantId,
        IReadOnlyList<string>? explicitRoles,
        IReadOnlyList<string>? explicitPermissions,
        string? explicitAccessToken,
        bool confirmed,
        string channel,
        string traceId);
}

/// <summary>Bearer / 请求体字段解析实现；可从 JWT payload 抽 sub/roles（尽力而为）。</summary>
public sealed class BearerIdentityResolver(ILogger<BearerIdentityResolver> logger) : IIdentityResolver
{
    /// <inheritdoc />
    public UserContext Resolve(
        HttpContext? http,
        string? explicitUserId,
        string? explicitTenantId,
        IReadOnlyList<string>? explicitRoles,
        IReadOnlyList<string>? explicitPermissions,
        string? explicitAccessToken,
        bool confirmed,
        string channel,
        string traceId)
    {
        var token = explicitAccessToken;
        if (string.IsNullOrWhiteSpace(token)
            && http?.Request.Headers.TryGetValue("Authorization", out var auth) == true)
        {
            var raw = auth.ToString();
            if (raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = raw["Bearer ".Length..].Trim();
            }
        }

        string? userId = explicitUserId;
        string? tenantId = explicitTenantId;
        var roles = new List<string>(explicitRoles ?? []);
        var permissions = new List<string>(explicitPermissions ?? []);
        string? displayName = null;

        if (!string.IsNullOrWhiteSpace(token))
        {
            TryEnrichFromJwtPayload(token!, ref userId, ref tenantId, ref displayName, roles, permissions);
        }

        return new UserContext
        {
            UserId = userId,
            TenantId = tenantId,
            DisplayName = displayName,
            Roles = roles.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            PermissionCodes = permissions.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            AccessToken = token,
            Channel = channel,
            Confirmed = confirmed,
            TraceId = traceId
        };
    }

    private void TryEnrichFromJwtPayload(
        string token,
        ref string? userId,
        ref string? tenantId,
        ref string? displayName,
        List<string> roles,
        List<string> permissions)
    {
        try
        {
            // 薄解析 payload claims；签名校验交给网关 / IdP，Agent 不做第二套认证中心。
            var parts = token.Split('.');
            if (parts.Length < 2)
            {
                return;
            }

            var json = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            userId ??= ReadString(root, "sub", "uid", "user_id", "nameid");
            tenantId ??= ReadString(root, "tenant_id", "tid", "org_id");
            displayName ??= ReadString(root, "name", "preferred_username");

            AddClaimValues(root, roles, "role", "roles");
            AddClaimValues(root, permissions, "permission", "permissions", "perm");
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "JWT payload parse skipped");
        }
    }

    private static string? ReadString(JsonElement root, params string[] names)
    {
        foreach (var n in names)
        {
            if (root.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.String)
            {
                var v = p.GetString();
                if (!string.IsNullOrWhiteSpace(v))
                {
                    return v;
                }
            }
        }

        return null;
    }

    private static void AddClaimValues(JsonElement root, List<string> target, params string[] names)
    {
        foreach (var n in names)
        {
            if (!root.TryGetProperty(n, out var p))
            {
                continue;
            }

            if (p.ValueKind == JsonValueKind.String)
            {
                SplitAdd(target, p.GetString());
            }
            else if (p.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in p.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        SplitAdd(target, item.GetString());
                    }
                }
            }
        }
    }

    private static void SplitAdd(List<string> target, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (var part in value.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries))
        {
            target.Add(part.Trim());
        }
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }

        return Convert.FromBase64String(s);
    }
}
