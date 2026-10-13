using System.Diagnostics;

namespace XW.Agent.Application.Telemetry;

/// <summary>单轮对话分析日志（车道、步骤、LLM Token）。</summary>
public interface IAgentTurnTelemetry
{
    void Begin(string traceId);
    void SetLane(string lane, string? reason = null);
    void SetPhase(string phase);
    void AddStep(string kind, string name, long elapsedMs, string? detail = null, bool? success = null);
    void AddLlmCall(AgentLlmCallUsage usage);
    AgentTurnSnapshot Snapshot(long totalElapsedMs);
    void Clear();
}

public sealed class AgentLlmCallUsage
{
    public string CallId { get; init; } = string.Empty;
    public string? Profile { get; init; }
    public string? Model { get; init; }
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public int TotalTokens { get; init; }
    public long ElapsedMs { get; init; }
}

public sealed class AgentTurnSnapshot
{
    public string TraceId { get; init; } = string.Empty;
    public string? Lane { get; init; }
    public string? LaneReason { get; init; }
    public string? Phase { get; init; }
    public long TotalElapsedMs { get; init; }
    public IReadOnlyList<AgentTurnStep> Steps { get; init; } = [];
    public IReadOnlyList<AgentLlmCallUsage> LlmCalls { get; init; } = [];
    public int PromptTokens => LlmCalls.Sum(x => x.PromptTokens);
    public int CompletionTokens => LlmCalls.Sum(x => x.CompletionTokens);
    public int TotalTokens => LlmCalls.Sum(x => x.TotalTokens > 0
        ? x.TotalTokens
        : x.PromptTokens + x.CompletionTokens);
}

public sealed class AgentTurnStep
{
    public string Kind { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public long ElapsedMs { get; init; }
    public string? Detail { get; init; }
    public bool? Success { get; init; }
}

/// <summary>基于 AsyncLocal 的单轮遥测（与 UserContext 同生命周期）。</summary>
public sealed class AsyncLocalAgentTurnTelemetry : IAgentTurnTelemetry
{
    private static readonly AsyncLocal<TurnState?> Holder = new();

    public void Begin(string traceId)
    {
        Holder.Value = new TurnState { TraceId = traceId, Started = Stopwatch.GetTimestamp() };
    }

    public void SetLane(string lane, string? reason = null)
    {
        var s = Holder.Value;
        if (s is null) return;
        s.Lane = lane;
        s.LaneReason = reason;
    }

    public void SetPhase(string phase)
    {
        var s = Holder.Value;
        if (s is null) return;
        s.Phase = phase;
    }

    public void AddStep(string kind, string name, long elapsedMs, string? detail = null, bool? success = null)
    {
        var s = Holder.Value;
        if (s is null) return;
        s.Steps.Add(new AgentTurnStep
        {
            Kind = kind,
            Name = name,
            ElapsedMs = elapsedMs,
            Detail = detail,
            Success = success
        });
    }

    public void AddLlmCall(AgentLlmCallUsage usage)
    {
        var s = Holder.Value;
        if (s is null) return;
        s.LlmCalls.Add(usage);
        s.Steps.Add(new AgentTurnStep
        {
            Kind = "llm",
            Name = usage.Profile ?? usage.CallId,
            ElapsedMs = usage.ElapsedMs,
            Detail = $"tokens={usage.TotalTokens} (p={usage.PromptTokens}/c={usage.CompletionTokens}) model={usage.Model}",
            Success = true
        });
    }

    public AgentTurnSnapshot Snapshot(long totalElapsedMs)
    {
        var s = Holder.Value;
        if (s is null)
        {
            return new AgentTurnSnapshot { TotalElapsedMs = totalElapsedMs };
        }

        return new AgentTurnSnapshot
        {
            TraceId = s.TraceId,
            Lane = s.Lane,
            LaneReason = s.LaneReason,
            Phase = s.Phase,
            TotalElapsedMs = totalElapsedMs,
            Steps = s.Steps.ToList(),
            LlmCalls = s.LlmCalls.ToList()
        };
    }

    public void Clear() => Holder.Value = null;

    private sealed class TurnState
    {
        public string TraceId { get; set; } = string.Empty;
        public string? Lane { get; set; }
        public string? LaneReason { get; set; }
        public string? Phase { get; set; }
        public long Started { get; set; }
        public List<AgentTurnStep> Steps { get; } = [];
        public List<AgentLlmCallUsage> LlmCalls { get; } = [];
    }
}
