using Microsoft.Extensions.Logging;
using XW.Agent.Application.Models;

namespace XW.Agent.Application.Intents;

/// <summary>
/// 极薄入口护栏：仅闲聊短路。其余一律进 Skill 循环（含 knowledge.search）。
/// 不再按故障词/政策词切 Knowledge 车道。
/// </summary>
public enum AgentRouteLane
{
    /// <summary>已废弃：知识检索改为 Skill，不再单独车道。保留枚举以免遥测/序列化中断。</summary>
    Knowledge = 0,

    /// <summary>Skill 循环：检索 → 规划 → 执行 → 观察。</summary>
    Action = 1,

    /// <summary>问候/空消息 → 短回复，不调 Skill。</summary>
    Chitchat = 2
}

/// <summary>入口护栏结果。</summary>
public sealed class AgentRouteDecision
{
    public required AgentRouteLane Lane { get; init; }

    public required string Intent { get; init; }

    public double Confidence { get; init; }

    public string Reason { get; init; } = string.Empty;
}

/// <summary>用户消息 → Chitchat | Skill（Action）。</summary>
public interface IAgentLaneRouter
{
    Task<AgentRouteDecision> RouteAsync(
        AgentSession session,
        string message,
        CancellationToken cancellationToken = default);
}

/// <summary>只识别空消息与短问候；其余交给 Planner 在 Skill 目录里选型或追问。</summary>
public sealed class RuleBasedLaneRouter(ILogger<RuleBasedLaneRouter> logger) : IAgentLaneRouter
{
    public Task<AgentRouteDecision> RouteAsync(
        AgentSession session,
        string message,
        CancellationToken cancellationToken = default)
    {
        _ = session;
        _ = cancellationToken;
        var text = (message ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return Task.FromResult(Decision(AgentRouteLane.Chitchat, "chitchat", 0.5, "empty_message"));
        }

        if (LooksLikeChitchat(text))
        {
            return Task.FromResult(Decision(AgentRouteLane.Chitchat, "chitchat", 0.85, "greeting_or_smalltalk"));
        }

        logger.LogDebug("Skill loop (no knowledge lane) messageLen={Len}", text.Length);
        return Task.FromResult(Decision(AgentRouteLane.Action, "agent_plan", 0.8, "skill_loop"));
    }

    private static AgentRouteDecision Decision(
        AgentRouteLane lane, string intent, double confidence, string reason) =>
        new()
        {
            Lane = lane,
            Intent = intent,
            Confidence = confidence,
            Reason = reason
        };

    private static bool LooksLikeChitchat(string text) =>
        ContainsAny(text, "你好", "您好", "在吗", "谢谢", "再见", "hello", "hi", "嗨")
        && text.Length <= 20
        && !ContainsAny(text, "查一下", "帮我", "创建", "报修");

    private static bool ContainsAny(string text, params string[] keys) =>
        keys.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
}
