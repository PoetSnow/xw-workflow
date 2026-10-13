using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XW.Agent.Application.Context;
using XW.Agent.Application.Options;
using XW.Agent.Application.Telemetry;

namespace XW.Agent.Application.Integration;

/// <summary>经集成网关发出的一次 HTTP 请求描述。</summary>
public sealed class IntegrationHttpRequest
{
    /// <summary>适配器：host / external。</summary>
    public required string Adapter { get; init; }

    /// <summary>HTTP 方法。</summary>
    public required string Method { get; init; }

    /// <summary>相对路径（可含 query）。</summary>
    public required string Path { get; init; }

    /// <summary>AuthProfile Code。</summary>
    public string? AuthProfileCode { get; init; }

    /// <summary>覆盖 BaseUrl（external 常用）。</summary>
    public string? OverrideBaseUrl { get; init; }

    /// <summary>请求体（可空）。</summary>
    public HttpContent? Content { get; init; }

    /// <summary>超时秒数。</summary>
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>GET 重试次数上限。</summary>
    public int MaxRetries { get; init; }
}

/// <summary>集成网关 HTTP 响应。</summary>
public sealed class IntegrationHttpResponse
{
    /// <summary>是否 2xx。</summary>
    public bool Ok { get; init; }

    /// <summary>HTTP 状态码。</summary>
    public int StatusCode { get; init; }

    /// <summary>响应正文。</summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>耗时毫秒。</summary>
    public long ElapsedMs { get; init; }

    /// <summary>错误描述（失败时）。</summary>
    public string? Error { get; init; }

    /// <summary>最终请求 URL（脱敏后）。</summary>
    public string FinalUrl { get; init; } = string.Empty;
}

/// <summary>
/// 集成网关：只通过 HTTP Adapter 调下游（Host / 客户 API），禁止直连库。
/// </summary>
public interface IIntegrationGateway
{
    /// <summary>按 AuthProfile 附加鉴权后发送 HTTP 请求。</summary>
    Task<IntegrationHttpResponse> SendAsync(
        IntegrationHttpRequest request,
        UserContext user,
        CancellationToken cancellationToken = default);
}

/// <summary>基于 HttpClient 的集成网关实现（含瞬态重试与鉴权注入）。</summary>
public sealed class HttpIntegrationGateway(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<AgentOptions> options,
    ILogger<HttpIntegrationGateway> logger) : IIntegrationGateway
{
    /// <summary>命名 HttpClient。</summary>
    public const string HttpClientName = "agent-integration";

    /// <inheritdoc />
    public async Task<IntegrationHttpResponse> SendAsync(
        IntegrationHttpRequest request,
        UserContext user,
        CancellationToken cancellationToken = default)
    {
        var opt = options.CurrentValue;
        var baseUrl = ResolveBaseUrl(opt, request);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return new IntegrationHttpResponse
            {
                Ok = false,
                Error = $"Adapter '{request.Adapter}' base URL is not configured",
                ElapsedMs = 0
            };
        }

        var path = request.Path.StartsWith('/') ? request.Path : "/" + request.Path;
        var url = baseUrl.TrimEnd('/') + path;
        var method = new HttpMethod(request.Method.Trim().ToUpperInvariant());
        // 写操作不重试：采购 POST 重放会另开一单。幂等键只保证下游去重，网关这里直接停。
        var retries = ResolveRetryCount(method.Method, request.MaxRetries);

        using var integrationActivity = AgentTelemetry.StartIntegration(
            request.Adapter, method.Method, path);
        integrationActivity?.SetTag(AgentTelemetry.Tags.TraceId, user.TraceId);

        Exception? lastEx = null;
        for (var attempt = 0; attempt <= retries; attempt++)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(request.TimeoutSeconds, 3, 120)));

                var client = httpClientFactory.CreateClient(HttpClientName);
                using var req = new HttpRequestMessage(method, url);
                if (request.Content is not null)
                {
                    req.Content = request.Content;
                }

                ApplyAuth(req, opt, request.AuthProfileCode, user);

                using var resp = await client.SendAsync(req, cts.Token);
                var body = await resp.Content.ReadAsStringAsync(cts.Token);
                sw.Stop();

                if (!resp.IsSuccessStatusCode && attempt < retries && IsTransient((int)resp.StatusCode))
                {
                    logger.LogWarning("Integration transient {Status} on {Url}, retry {Attempt}",
                        (int)resp.StatusCode, RedactUrl(url), attempt + 1);
                    await Task.Delay(100 * (attempt + 1), cancellationToken);
                    continue;
                }

                integrationActivity?.SetTag(AgentTelemetry.Tags.IntegrationStatus, (int)resp.StatusCode);
                integrationActivity?.SetTag("agent.integration.elapsed_ms", sw.ElapsedMilliseconds);
                AgentTelemetry.SetOk(integrationActivity, resp.IsSuccessStatusCode);

                return new IntegrationHttpResponse
                {
                    Ok = resp.IsSuccessStatusCode,
                    StatusCode = (int)resp.StatusCode,
                    Body = body,
                    ElapsedMs = sw.ElapsedMilliseconds,
                    FinalUrl = RedactUrl(url),
                    Error = resp.IsSuccessStatusCode ? null : $"HTTP {(int)resp.StatusCode}"
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // 聊天/上传的服务端截止时间到了：不要当成下游失败再走下一步工具。
                throw;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                sw.Stop();
                AgentTelemetry.SetOk(integrationActivity, false);
                integrationActivity?.SetTag(AgentTelemetry.Tags.IntegrationStatus, "timeout");
                return new IntegrationHttpResponse
                {
                    Ok = false,
                    Error = "timeout",
                    ElapsedMs = sw.ElapsedMilliseconds,
                    FinalUrl = RedactUrl(url)
                };
            }
            catch (Exception ex) when (attempt < retries)
            {
                lastEx = ex;
                logger.LogWarning(ex, "Integration attempt {Attempt} failed for {Url}", attempt + 1, RedactUrl(url));
                await Task.Delay(100 * (attempt + 1), cancellationToken);
            }
            catch (Exception ex)
            {
                sw.Stop();
                AgentTelemetry.SetError(integrationActivity, ex);
                return new IntegrationHttpResponse
                {
                    Ok = false,
                    Error = ex.Message,
                    ElapsedMs = sw.ElapsedMilliseconds,
                    FinalUrl = RedactUrl(url)
                };
            }
        }

        AgentTelemetry.SetOk(integrationActivity, false);
        return new IntegrationHttpResponse
        {
            Ok = false,
            Error = lastEx?.Message ?? "integration_failed",
            FinalUrl = RedactUrl(url)
        };
    }

    /// <summary>
    /// GET 才重试，次数钳在 0–2。其它方法固定 0。
    /// 采购创建是 POST，超时或 5xx 时不能自动再打一枪。
    /// </summary>
    public static int ResolveRetryCount(string method, int maxRetries)
    {
        if (!string.Equals(method.Trim(), "GET", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return Math.Clamp(maxRetries, 0, 2);
    }

    private static string ResolveBaseUrl(AgentOptions opt, IntegrationHttpRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.OverrideBaseUrl))
        {
            return request.OverrideBaseUrl!;
        }

        if (string.Equals(request.Adapter, "host", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(request.Adapter))
        {
            return opt.HostApi.BaseUrl;
        }

        // external：必须在 Capability.Http.BaseUrl 指定
        return request.OverrideBaseUrl ?? string.Empty;
    }

    private static void ApplyAuth(
        HttpRequestMessage req,
        AgentOptions opt,
        string? authProfileCode,
        UserContext user)
    {
        var code = authProfileCode;
        if (string.IsNullOrWhiteSpace(code))
        {
            // 兼容旧 HostApi.AccessToken
            if (!string.IsNullOrWhiteSpace(opt.HostApi.AccessToken))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opt.HostApi.AccessToken);
            }

            return;
        }

        var profile = opt.AuthProfiles.FirstOrDefault(p =>
            string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            return;
        }

        switch (profile.Type.Trim().ToLowerInvariant())
        {
            case "none":
                break;
            case "bearer":
                var bearer = !string.IsNullOrWhiteSpace(profile.Token)
                    ? profile.Token
                    : opt.HostApi.AccessToken;
                if (!string.IsNullOrWhiteSpace(bearer))
                {
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
                }

                break;
            case "api_key_header":
                var header = string.IsNullOrWhiteSpace(profile.HeaderName) ? "X-Api-Key" : profile.HeaderName!;
                if (!string.IsNullOrWhiteSpace(profile.Token))
                {
                    req.Headers.TryAddWithoutValidation(header, profile.Token);
                }

                break;
            case "forward_caller":
                if (!string.IsNullOrWhiteSpace(user.AccessToken))
                {
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
                }

                break;
            default:
                break;
        }

        // 可选用户上下文头（下游做数据权限 / 审计）
        if (!string.IsNullOrWhiteSpace(user.UserId))
        {
            req.Headers.TryAddWithoutValidation("X-Agent-User-Id", user.UserId);
        }

        if (!string.IsNullOrWhiteSpace(user.TenantId))
        {
            req.Headers.TryAddWithoutValidation("X-Agent-Tenant-Id", user.TenantId);
        }

        if (!string.IsNullOrWhiteSpace(user.TraceId))
        {
            req.Headers.TryAddWithoutValidation("X-Agent-Trace-Id", user.TraceId);
        }
    }

    private static bool IsTransient(int status) =>
        status is 408 or 429 or >= 500;

    private static string RedactUrl(string url)
    {
        // 避免把 query 里的临时 token 打进日志（简单截断）
        var q = url.IndexOf('?', StringComparison.Ordinal);
        return q < 0 ? url : url[..q] + "?…";
    }
}
