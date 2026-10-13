using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XW.Agent.Application.Abstractions;
using XW.Agent.Application.Context;
using XW.Agent.Application.Integration;
using XW.Agent.Application.Models;
using XW.Agent.Application.Options;

namespace XW.Agent.Application.Tooling;

/// <summary>能力目录：列出/查找已启用的 Capability 定义（业务或技术视图由注册中心决定）。</summary>
public interface ICapabilityCatalog
{
    /// <summary>全部启用的能力。</summary>
    IReadOnlyList<AgentCapabilityDefinition> ListEnabled();

    /// <summary>按 Code 查找。</summary>
    AgentCapabilityDefinition? FindByCode(string code);

    /// <summary>按 IntentCode 查找。</summary>
    AgentCapabilityDefinition? FindByIntent(string intentCode);
}

/// <summary>配置驱动 HTTP Capability（经 Integration 网关，支持 AuthProfile）。</summary>
public sealed class HttpCapabilityTool(
    AgentCapabilityDefinition definition,
    IIntegrationGateway integration,
    IUserContextAccessor userContext,
    IAgentTool? stubFallback,
    ILogger logger) : IAgentTool
{
    private static readonly Regex Placeholder = new(@"\{\{\s*(?<name>[a-zA-Z0-9_]+)\s*\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <inheritdoc />
    public string Name => definition.Code;
    /// <inheritdoc />
    public string Description => definition.Description;
    /// <inheritdoc />
    public string RequiredPermission =>
        definition.ResolvePermissions().FirstOrDefault() ?? definition.Permission;
    /// <inheritdoc />
    public IReadOnlyList<string> RequiredSlots =>
        definition.Parameters.Where(p => p.Required).Select(p => p.Name).ToList();

    /// <summary>是否需要二次确认。</summary>
    public bool ConfirmRequired => definition.ConfirmRequired;

    /// <inheritdoc />
    /// <remarks>4xx 禁止 FallbackToStub；成功时 Data 含 body/preview 供 Workflow 抽 id。</remarks>
    public async Task<ToolExecutionResult> ExecuteAsync(
        AgentSession session,
        IntentResult intent,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var slots = MergeSlots(session, intent);
        var user = userContext.Current ?? new UserContext { TraceId = "unknown" };

        if (definition.Http is null)
        {
            return await FallbackOrFailAsync(session, intent, sw, "Capability 未配置 HTTP 绑定", ct);
        }

        try
        {
            var http = definition.Http;
            var method = http.Method.Trim().ToUpperInvariant();
            var path = Render(http.Path, slots);
            var query = BuildQuery(http.Query, slots);
            if (!string.IsNullOrEmpty(query))
            {
                path += (path.Contains('?', StringComparison.Ordinal) ? "&" : "?") + query;
            }

            HttpContent? content = null;
            string? renderedBody = null;
            if (method is not ("GET" or "HEAD")
                && !string.IsNullOrWhiteSpace(http.BodyTemplate))
            {
                EnsureIdempotencySlots(slots);
                if (slots.TryGetValue("_pending.idempotencyKey", out var pendingIdem)
                    && !string.IsNullOrWhiteSpace(pendingIdem))
                {
                    slots["idempotencyKey"] = pendingIdem;
                }

                if (slots.TryGetValue("_pending.lineIdempotencyKey", out var pendingLine)
                    && !string.IsNullOrWhiteSpace(pendingLine))
                {
                    slots["lineIdempotencyKey"] = pendingLine;
                }

                try
                {
                    renderedBody = RenderJsonBody(http.BodyTemplate!, slots);
                }
                catch (InvalidOperationException ex)
                {
                    sw.Stop();
                    return new ToolExecutionResult
                    {
                        ToolName = Name,
                        Success = false,
                        ElapsedMs = sw.ElapsedMilliseconds,
                        Error = ex.Message,
                        Summary = $"创建失败：{ex.Message}"
                    };
                }
                catch (JsonException ex)
                {
                    sw.Stop();
                    return new ToolExecutionResult
                    {
                        ToolName = Name,
                        Success = false,
                        ElapsedMs = sw.ElapsedMilliseconds,
                        Error = ex.Message,
                        Summary = $"请求体不是合法 JSON：{ex.Message}"
                    };
                }

                content = new StringContent(renderedBody, Encoding.UTF8, "application/json");
                logger.LogInformation(
                    "Capability {Code} POST body={Body}",
                    definition.Code, Truncate(renderedBody, 500));
            }

            var resp = await integration.SendAsync(new IntegrationHttpRequest
            {
                Adapter = string.IsNullOrWhiteSpace(http.Adapter) ? "host" : http.Adapter,
                Method = method,
                Path = path,
                AuthProfileCode = http.AuthProfile,
                OverrideBaseUrl = http.BaseUrl,
                Content = content,
                TimeoutSeconds = http.TimeoutSeconds > 0 ? http.TimeoutSeconds : 30,
                MaxRetries = http.MaxRetries
            }, user, ct);

            sw.Stop();

            if (!resp.Ok)
            {
                var detail = ExtractHttpProblemDetail(resp.Body);
                var httpError = FormatHttpError(resp.StatusCode, detail ?? resp.Error);
                var authFail = IsAuthFailure(resp.StatusCode);
                logger.LogWarning(
                    "Capability {Code} failed: {Error} status={Status} auth={Auth} body={Body}",
                    definition.Code, httpError, resp.StatusCode, authFail, Truncate(resp.Body, 400));

                // 4xx = 请求/鉴权/参数问题，不是下游宕机：禁止 FallbackToStub，避免假成功骗人
                if (!IsClientError(resp.StatusCode) && http.FallbackToStub && stubFallback is not null)
                {
                    var stub = await stubFallback.ExecuteAsync(session, intent, ct);
                    stub.Summary = $"下游失败({httpError})，已回退 stub。{stub.Summary}";
                    return stub;
                }

                return new ToolExecutionResult
                {
                    ToolName = Name,
                    Success = false,
                    ElapsedMs = resp.ElapsedMs,
                    HttpStatusCode = resp.StatusCode > 0 ? resp.StatusCode : null,
                    Error = httpError,
                    Summary = IsAuthFailure(resp.StatusCode)
                        ? $"鉴权失败：{httpError}（未回退 stub，请检查 Token/权限）"
                        : IsClientError(resp.StatusCode)
                            ? $"创建/调用失败：{httpError}"
                            : $"调用下游失败：{httpError}",
                    Data = string.IsNullOrWhiteSpace(resp.Body)
                        ? null
                        : new { body = Truncate(resp.Body, 2000) }
                };
            }

            return new ToolExecutionResult
            {
                ToolName = Name,
                Success = true,
                ElapsedMs = resp.ElapsedMs,
                Summary = BuildSuccessSummary(resp.Body, slots),
                Data = new
                {
                    source = http.Adapter,
                    capability = definition.Code,
                    url = resp.FinalUrl,
                    preview = Truncate(resp.Body, 1500),
                    body = Truncate(resp.Body, 4000)
                }
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            logger.LogWarning(ex, "Capability {Code} execution error", definition.Code);
            return await FallbackOrFailAsync(session, intent, sw, ex.Message, ct);
        }
    }

    private async Task<ToolExecutionResult> FallbackOrFailAsync(
        AgentSession session,
        IntentResult intent,
        Stopwatch sw,
        string reason,
        CancellationToken ct)
    {
        if (definition.Http?.FallbackToStub == true && stubFallback is not null)
        {
            var stub = await stubFallback.ExecuteAsync(session, intent, ct);
            stub.Summary = $"{reason}，已回退 stub。{stub.Summary}";
            return stub;
        }

        return new ToolExecutionResult
        {
            ToolName = Name,
            Success = false,
            ElapsedMs = sw.ElapsedMilliseconds,
            Error = reason,
            Summary = $"能力 {definition.Code} 执行失败：{reason}"
        };
    }

    /// <summary>HTTP 401/403：鉴权/权限失败，不可当作下游不可用。</summary>
    private static bool IsAuthFailure(int statusCode) => statusCode is 401 or 403;

    /// <summary>4xx：客户端/契约错误，禁止 Stub 伪装成功。</summary>
    private static bool IsClientError(int statusCode) => statusCode is >= 400 and < 500;

    private static string FormatHttpError(int statusCode, string? error)
    {
        if (statusCode > 0)
        {
            var label = $"HTTP {statusCode}";
            if (!string.IsNullOrWhiteSpace(error)
                && !error.Contains(label, StringComparison.OrdinalIgnoreCase))
            {
                return $"{label}: {error}";
            }

            return string.IsNullOrWhiteSpace(error) ? label : error.Trim();
        }

        return string.IsNullOrWhiteSpace(error) ? "HTTP_error" : error.Trim();
    }

    /// <summary>从 ASP.NET ProblemDetails / 业务错误体抽取可读 detail。</summary>
    private static string? ExtractHttpProblemDetail(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            foreach (var key in new[] { "detail", "Detail", "title", "Title", "message", "Message", "error" })
            {
                if (root.TryGetProperty(key, out var el)
                    && el.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(el.GetString()))
                {
                    return el.GetString()!.Trim();
                }
            }

            // errors: { field: ["msg"] }
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var parts = new List<string>();
                foreach (var prop in errors.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in prop.Value.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String
                                && !string.IsNullOrWhiteSpace(item.GetString()))
                            {
                                parts.Add($"{prop.Name}: {item.GetString()}");
                            }
                        }
                    }
                }

                if (parts.Count > 0)
                {
                    return string.Join("；", parts.Take(5));
                }
            }
        }
        catch
        {
            // 非 JSON
        }

        var trimmed = body.Trim();
        return trimmed.Length <= 240 ? trimmed : trimmed[..240] + "...";
    }

    private static Dictionary<string, string> MergeSlots(AgentSession session, IntentResult intent)
    {
        var slots = new Dictionary<string, string>(session.Slots, StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in intent.Slots)
        {
            if (!string.IsNullOrWhiteSpace(v))
            {
                slots[k] = v;
            }
        }

        return slots;
    }

    private static void EnsureIdempotencySlots(Dictionary<string, string> slots)
    {
        if (!slots.TryGetValue("idempotencyKey", out var key) || string.IsNullOrWhiteSpace(key))
        {
            slots["idempotencyKey"] = Guid.NewGuid().ToString("N");
        }

        if (!slots.TryGetValue("lineIdempotencyKey", out var lineKey) || string.IsNullOrWhiteSpace(lineKey))
        {
            slots["lineIdempotencyKey"] = Guid.NewGuid().ToString("N");
        }

        // 空备注会导致 JSON 缺值时仍合法（空字符串）
        if (!slots.ContainsKey("remark"))
        {
            slots["remark"] = string.Empty;
        }
    }

    private static string RenderJsonBody(string template, Dictionary<string, string> slots)
    {
        var rendered = Render(template, slots);
        if (Placeholder.IsMatch(rendered))
        {
            throw new InvalidOperationException("请求体仍有未填充参数，拒绝调用。");
        }

        using var _ = JsonDocument.Parse(rendered);
        return rendered;
    }

    private static string BuildQuery(Dictionary<string, string> queryTemplate, Dictionary<string, string> slots)
    {
        var parts = new List<string>();
        foreach (var (key, template) in queryTemplate)
        {
            var value = Render(template, slots);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            parts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
        }

        return string.Join("&", parts);
    }

    private static string Render(string template, Dictionary<string, string> slots) =>
        Placeholder.Replace(template, m =>
        {
            var name = m.Groups["name"].Value;
            return slots.TryGetValue(name, out var val) && !string.IsNullOrWhiteSpace(val)
                ? val.Trim()
                : string.Empty;
        });

    private string BuildSuccessSummary(string raw, Dictionary<string, string> slots)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.TryGetProperty("data", out var dataEl))
            {
                root = dataEl;
            }

            // 比价结果：bySupplier 数组
            if (root.TryGetProperty("bySupplier", out var bySupplier)
                && bySupplier.ValueKind == JsonValueKind.Array)
            {
                var n = bySupplier.GetArrayLength();
                if (n == 0)
                {
                    return $"{definition.Code}：暂无历史采购价。";
                }

                var previews = new List<string>();
                foreach (var item in bySupplier.EnumerateArray().Take(5))
                {
                    var name = item.TryGetProperty("supplierName", out var sn) ? sn.GetString() : null;
                    var sid = item.TryGetProperty("supplierId", out var sidEl) ? sidEl.ToString() : "?";
                    var min = item.TryGetProperty("minUnitPrice", out var minEl) ? minEl.ToString() : "?";
                    var last = item.TryGetProperty("lastUnitPrice", out var lastEl) ? lastEl.ToString() : "?";
                    previews.Add($"供应商Id={sid}「{name ?? "?"}」最低{min} 最近{last}");
                }

                return $"{definition.Code}：{n} 家供应商。{string.Join("；", previews)}";
            }

            // 分页列表：根为数组，或 PageModelDto.list / data / items
            JsonElement list = default;
            var hasList = false;
            if (root.ValueKind == JsonValueKind.Array)
            {
                list = root;
                hasList = true;
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var key in new[] { "list", "items", "data", "rows" })
                {
                    if (!root.TryGetProperty(key, out var nested) || nested.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    list = nested;
                    hasList = true;
                    break;
                }

                // data: { list: [...] }
                if (!hasList
                    && root.TryGetProperty("data", out var dataObj)
                    && dataObj.ValueKind == JsonValueKind.Object)
                {
                    foreach (var key in new[] { "list", "items", "data", "rows" })
                    {
                        if (!dataObj.TryGetProperty(key, out var nested) || nested.ValueKind != JsonValueKind.Array)
                        {
                            continue;
                        }

                        list = nested;
                        hasList = true;
                        break;
                    }
                }
            }

            if (hasList)
            {
                var data = list;
                var total = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("totalCount", out var tc)
                    ? tc.GetInt32()
                    : root.ValueKind == JsonValueKind.Object && root.TryGetProperty("total", out var t)
                        ? t.GetInt32()
                        : data.GetArrayLength();

                if (data.GetArrayLength() == 0)
                {
                    return $"{definition.Code}：0 条。";
                }

                var listPreviews = new List<string>();
                foreach (var item in data.EnumerateArray().Take(5))
                {
                    // 库存账行有 id 但无 name：优先展示仓/现存，避免误报 id=… name=-
                    var wh = item.TryGetProperty("warehouseId", out var w) ? w.ToString() : null;
                    var onHand = item.TryGetProperty("qtyOnHand", out var qh) ? qh.ToString() : null;
                    if (wh is not null || onHand is not null)
                    {
                        var reserved = item.TryGetProperty("qtyReserved", out var qr) ? qr.ToString() : "?";
                        listPreviews.Add($"仓{wh ?? "-"} 现存{onHand ?? "?"} 预留{reserved}");
                        continue;
                    }

                    var id = item.TryGetProperty("id", out var idEl) ? idEl.ToString() : null;
                    var code = item.TryGetProperty("code", out var c) ? c.GetString() : null;
                    var name = item.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (id is not null || code is not null || name is not null)
                    {
                        listPreviews.Add($"id={id ?? "-"} code={code ?? "-"} name={name ?? "-"}");
                    }
                }

                return listPreviews.Count == 0
                    ? $"{definition.Code}：共 {total} 条。"
                    : $"{definition.Code}：共 {total} 条。{string.Join("；", listPreviews)}";
            }
        }
        catch
        {
            // fall through
        }

        return $"{definition.Code} 已返回：{Truncate(raw, 240)}";
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? string.Empty : s.Length <= max ? s : s[..max] + "...";
}

/// <summary>
/// 工具注册表：优先按 Capability 配置构造 <see cref="HttpCapabilityTool"/>，
/// 无 HTTP 绑定时回退到同名 Stub。
/// </summary>
public sealed class CapabilityToolRegistry : IToolRegistry
{
    private readonly ICapabilityCatalog _catalog;
    private readonly IIntegrationGateway _integration;
    private readonly IUserContextAccessor _userContext;
    private readonly Dictionary<string, IAgentTool> _stubs;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IOptionsMonitor<AgentOptions> _options;

    public CapabilityToolRegistry(
        ICapabilityCatalog catalog,
        IIntegrationGateway integration,
        IUserContextAccessor userContext,
        IEnumerable<IAgentTool> stubTools,
        ILoggerFactory loggerFactory,
        IOptionsMonitor<AgentOptions> options)
    {
        _catalog = catalog;
        _integration = integration;
        _userContext = userContext;
        _loggerFactory = loggerFactory;
        _options = options;
        _stubs = stubTools
            .Where(t => t is not HttpCapabilityTool)
            .GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<IAgentTool> All =>
        _catalog.ListEnabled().Select(ResolveTool).ToList();

    public IAgentTool? FindByIntent(string intent)
    {
        var intentDef = _options.CurrentValue.Intents.FirstOrDefault(i =>
            string.Equals(i.Code, intent, StringComparison.OrdinalIgnoreCase));

        var cap = _catalog.FindByIntent(intent)
                  ?? (intentDef?.ToolName is { } toolName ? _catalog.FindByCode(toolName) : null);

        if (cap is not null)
        {
            return ResolveTool(cap);
        }

        if (intentDef?.ToolName is not null && _stubs.TryGetValue(intentDef.ToolName, out var stub))
        {
            return stub;
        }

        return null;
    }

    public IAgentTool? FindByCode(string code)
    {
        var cap = _catalog.FindByCode(code);
        if (cap is not null)
        {
            return ResolveTool(cap);
        }

        return _stubs.TryGetValue(code, out var stub) ? stub : null;
    }

    private IAgentTool ResolveTool(AgentCapabilityDefinition cap)
    {
        _stubs.TryGetValue(cap.Code, out var stub);
        if (cap.Http is not null)
        {
            return new HttpCapabilityTool(
                cap,
                _integration,
                _userContext,
                stub,
                _loggerFactory.CreateLogger($"Capability.{cap.Code}"));
        }

        return stub ?? new MissingCapabilityTool(cap);
    }

    private sealed class MissingCapabilityTool(AgentCapabilityDefinition cap) : IAgentTool
    {
        public string Name => cap.Code;
        public string Description => cap.Description;
        public string RequiredPermission =>
            cap.ResolvePermissions().FirstOrDefault() ?? cap.Permission;
        public IReadOnlyList<string> RequiredSlots =>
            cap.Parameters.Where(p => p.Required).Select(p => p.Name).ToList();

        public Task<ToolExecutionResult> ExecuteAsync(
            AgentSession session, IntentResult intent, CancellationToken ct = default) =>
            Task.FromResult(new ToolExecutionResult
            {
                ToolName = Name,
                Success = false,
                Error = "no_http_binding",
                Summary = $"能力 {cap.Code} 尚未配置 HTTP 绑定，也没有 stub。"
            });
    }
}
