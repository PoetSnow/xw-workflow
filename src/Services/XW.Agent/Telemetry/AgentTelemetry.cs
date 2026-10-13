using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace XW.Agent.Application.Telemetry;

/// <summary>
/// Agent Trace：BCL ActivitySource，由 Host 的 OpenTelemetry 订阅导出。
/// Sprint 1：一次 chat 请求可见 Retrieve → Plan → LLM → Authorize → Execute → Integration。
/// </summary>
public static class AgentTelemetry
{
    public const string ServiceName = "xw-agent";
    public const string ActivitySourceName = "XW.Agent";

    public static readonly ActivitySource Source = new(ActivitySourceName, "1.0.0");

    /// <summary>
    /// 指标与 ActivitySource 并列。标签只用 lane、phase、工具码、HTTP 状态类。
    /// 不放用户原文、访问令牌或幂等键。
    /// </summary>
    public static readonly Meter Meter = new(ServiceName, "1.0.0");

    private static readonly Counter<long> ChatCount = Meter.CreateCounter<long>("xw.agent.chat");
    private static readonly Counter<long> ToolCount = Meter.CreateCounter<long>("xw.agent.tool");
    private static readonly Counter<long> PermissionDeniedCount = Meter.CreateCounter<long>("xw.agent.permission_denied");
    private static readonly Counter<long> ApprovalRequiredCount = Meter.CreateCounter<long>("xw.agent.approval_required");
    private static readonly Counter<long> LlmFailureCount = Meter.CreateCounter<long>("xw.agent.llm.failure");
    private static readonly Histogram<double> TurnElapsed = Meter.CreateHistogram<double>("xw.agent.turn.elapsed_ms");
    private static readonly Histogram<double> ToolElapsed = Meter.CreateHistogram<double>("xw.agent.tool.elapsed_ms");
    private static readonly Histogram<long> LlmTokens = Meter.CreateHistogram<long>("xw.agent.llm.tokens");

    public static class Tags
    {
        public const string TraceId = "agent.trace_id";
        public const string SessionId = "agent.session_id";
        public const string UserId = "agent.user_id";
        public const string TenantId = "agent.tenant_id";
        public const string Phase = "agent.phase";
        public const string State = "agent.state";
        public const string Step = "agent.step";
        public const string Capability = "agent.capability";
        public const string Tool = "agent.tool";
        public const string Intent = "agent.intent";
        public const string Action = "agent.action";
        public const string PermissionDenied = "agent.permission_denied";
        public const string ApprovalRequired = "agent.approval_required";
        public const string LlmProfile = "agent.llm.profile";
        public const string LlmModel = "agent.llm.model";
        public const string LlmMode = "agent.llm.mode";
        public const string RetrieveHitCount = "agent.retrieve.hit_count";
        public const string RetrieveCandidateCount = "agent.retrieve.candidate_count";
        public const string IntegrationAdapter = "agent.integration.adapter";
        public const string IntegrationMethod = "agent.integration.method";
        public const string IntegrationPath = "agent.integration.path";
        public const string IntegrationStatus = "agent.integration.status";
    }

    public static Activity? StartChat(string traceId, string? sessionId, string? userId, string? tenantId)
    {
        var activity = Source.StartActivity("agent.chat", ActivityKind.Server);
        if (activity is null)
        {
            return null;
        }

        activity.SetTag(Tags.TraceId, traceId);
        SetIfPresent(activity, Tags.SessionId, sessionId);
        SetIfPresent(activity, Tags.UserId, userId);
        SetIfPresent(activity, Tags.TenantId, tenantId);
        return activity;
    }

    public static Activity? StartState(string stateName, int? step = null)
    {
        var activity = Source.StartActivity($"agent.{stateName}", ActivityKind.Internal);
        activity?.SetTag(Tags.State, stateName);
        if (step.HasValue)
        {
            activity?.SetTag(Tags.Step, step.Value);
        }

        return activity;
    }

    public static Activity? StartLlm(string profile, string model, string mode)
    {
        var activity = Source.StartActivity("agent.llm", ActivityKind.Client);
        if (activity is null)
        {
            return null;
        }

        activity.SetTag(Tags.LlmProfile, profile);
        activity.SetTag(Tags.LlmModel, model);
        activity.SetTag(Tags.LlmMode, mode);
        return activity;
    }

    public static Activity? StartIntegration(string adapter, string method, string path)
    {
        var activity = Source.StartActivity("agent.integration", ActivityKind.Client);
        if (activity is null)
        {
            return null;
        }

        activity.SetTag(Tags.IntegrationAdapter, adapter);
        activity.SetTag(Tags.IntegrationMethod, method);
        activity.SetTag(Tags.IntegrationPath, TruncatePath(path));
        return activity;
    }

    public static void SetOk(Activity? activity, bool ok)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetStatus(ok ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
    }

    /// <summary>一轮聊天结束时记次数和整轮耗时。</summary>
    public static void RecordChat(string? lane, string? phase, long elapsedMs)
    {
        var tags = new TagList
        {
            { "lane", NormalizeTag(lane) },
            { "phase", NormalizeTag(phase) }
        };
        ChatCount.Add(1, tags);
        TurnElapsed.Record(elapsedMs, tags);
    }

    /// <summary>单次工具执行。http_class 为 2xx/4xx/5xx/none，不放具体路径。</summary>
    public static void RecordTool(string? toolCode, bool success, long elapsedMs, int? httpStatus)
    {
        var tags = new TagList
        {
            { "tool", NormalizeTag(toolCode) },
            { "outcome", success ? "success" : "fail" },
            { "http_class", HttpClass(httpStatus) }
        };
        ToolCount.Add(1, tags);
        ToolElapsed.Record(elapsedMs, tags);
    }

    public static void RecordPermissionDenied(string? lane) =>
        PermissionDeniedCount.Add(1, new TagList { { "lane", NormalizeTag(lane) } });

    public static void RecordApprovalRequired(string? lane) =>
        ApprovalRequiredCount.Add(1, new TagList { { "lane", NormalizeTag(lane) } });

    public static void RecordLlmFailure(int? httpStatus) =>
        LlmFailureCount.Add(1, new TagList { { "http_class", HttpClass(httpStatus) } });

    public static void RecordLlmTokens(long totalTokens, string? lane)
    {
        if (totalTokens <= 0)
        {
            return;
        }

        LlmTokens.Record(totalTokens, new TagList { { "lane", NormalizeTag(lane) } });
    }

    /// <summary>把 HTTP 状态收成类别，避免每个状态码变成一个时间序列。</summary>
    public static string HttpClass(int? status) => status switch
    {
        null or <= 0 => "none",
        < 200 => "1xx",
        < 300 => "2xx",
        < 400 => "3xx",
        < 500 => "4xx",
        _ => "5xx"
    };

    public static void SetError(Activity? activity, Exception ex)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity.SetTag("exception.type", ex.GetType().FullName);
        activity.SetTag("exception.message", ex.Message);
    }

    private static void SetIfPresent(Activity activity, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            activity.SetTag(key, value);
        }
    }

    private static string NormalizeTag(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var trimmed = value.Trim();
        return trimmed.Length <= 64 ? trimmed : trimmed[..64];
    }

    private static string TruncatePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        var q = path.IndexOf('?', StringComparison.Ordinal);
        return q < 0 ? path : path[..q];
    }
}
