using XW.Agent.Application.Models;
using XW.Agent.Application.Options;

namespace XW.Agent.Application.Planning;

/// <summary>LLM 规划器单步决策（ReAct 风格）。</summary>
public sealed class AgentPlanDecision
{
    /// <summary>动作：call_tool | clarify | confirm | finish。</summary>
    public required string Action { get; init; }

    /// <summary>call_tool 时填写的业务能力码（Capability.Code）。</summary>
    public string? Tool { get; init; }

    /// <summary>调用参数（仅允许文档中声明的参数名）。</summary>
    public Dictionary<string, string> Arguments { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>clarify / confirm 时给用户的问题。</summary>
    public string? Question { get; init; }

    /// <summary>内部简短理由（不直接展示给用户）。</summary>
    public string? Reason { get; init; }
}

/// <summary>已执行一步的观察结果，反馈给下一轮 Planner。</summary>
public sealed class AgentPlanObservation
{
    /// <summary>业务能力码。</summary>
    public required string Tool { get; init; }

    /// <summary>是否成功。</summary>
    public bool Success { get; init; }

    /// <summary>摘要文本。</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>结果预览（截断 JSON）。</summary>
    public string Preview { get; init; } = string.Empty;

    /// <summary>错误信息（如 HTTP 401）。</summary>
    public string? Error { get; init; }

    /// <summary>下游 HTTP 状态码；401/403 表示鉴权/权限失败。</summary>
    public int? HttpStatusCode { get; init; }

    /// <summary>错误分类：auth | http | empty | stub | assign | …</summary>
    public string? ErrorKind { get; init; }

    /// <summary>本步调用参数（用于禁止同参死循环）。</summary>
    public Dictionary<string, string>? Arguments { get; init; }
}

/// <summary>
/// 编排规划器：根据候选能力文档 + 历史观察结果，决定下一步动作。
/// 一句话拆任务见 <see cref="IAgentTaskPlanner.CreatePlanAsync"/>（Phase 1）。
/// </summary>
public interface IAgentPlanner
{
    /// <summary>产出下一步决策（只规划一步，不直接执行工具）。</summary>
    Task<AgentPlanDecision> NextAsync(
        AgentSession session,
        string userMessage,
        IReadOnlyList<AgentCapabilityDefinition> tools,
        IReadOnlyList<AgentPlanObservation> observations,
        string? knowledgeContext = null,
        CancellationToken ct = default);
}
