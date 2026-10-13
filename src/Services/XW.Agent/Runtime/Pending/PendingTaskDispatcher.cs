using XW.Agent.Application.Models;
using XW.Agent.Application.Options;
using XW.Agent.Application.Planning;

namespace XW.Agent.Application.Runtime.Pending;

/// <summary>按 pending.kind 分发到对应 Handler。</summary>
public interface IPendingTaskDispatcher
{
    IPendingTaskHandler? Find(string? kind);

    bool TryPlan(
        AgentSession session,
        PendingTaskReplyKind reply,
        IReadOnlyList<AgentCapabilityDefinition> allowedCaps,
        out AgentPlanDecision decision);

    bool TryAfterTool(
        AgentSession session,
        IReadOnlyList<AgentCapabilityDefinition> allowedCaps,
        IReadOnlyList<AgentPlanObservation> observations,
        IReadOnlyList<string> summaries,
        out AgentPlanDecision decision);

    void NotifyToolSuccess(AgentSession session, string toolCode, object? data);

    void Cancel(AgentSession session);
}

public sealed class PendingTaskDispatcher(IEnumerable<IPendingTaskHandler> handlers) : IPendingTaskDispatcher
{
    private readonly Dictionary<string, IPendingTaskHandler> _map =
        handlers.ToDictionary(h => h.Kind, StringComparer.OrdinalIgnoreCase);

    public IPendingTaskHandler? Find(string? kind) =>
        string.IsNullOrWhiteSpace(kind) ? null
        : _map.TryGetValue(kind, out var h) ? h : null;

    public bool TryPlan(
        AgentSession session,
        PendingTaskReplyKind reply,
        IReadOnlyList<AgentCapabilityDefinition> allowedCaps,
        out AgentPlanDecision decision)
    {
        decision = null!;
        var handler = Find(PendingTaskSnapshot.GetKind(session));
        return handler is not null && handler.TryPlan(session, reply, allowedCaps, out decision);
    }

    public bool TryAfterTool(
        AgentSession session,
        IReadOnlyList<AgentCapabilityDefinition> allowedCaps,
        IReadOnlyList<AgentPlanObservation> observations,
        IReadOnlyList<string> summaries,
        out AgentPlanDecision decision)
    {
        decision = null!;
        var handler = Find(PendingTaskSnapshot.GetKind(session));
        return handler is not null
               && handler.TryAfterTool(session, allowedCaps, observations, summaries, out decision);
    }

    public void NotifyToolSuccess(AgentSession session, string toolCode, object? data)
    {
        Find(PendingTaskSnapshot.GetKind(session))?.OnToolSuccess(session, toolCode, data);
    }

    public void Cancel(AgentSession session)
    {
        var handler = Find(PendingTaskSnapshot.GetKind(session));
        if (handler is not null)
        {
            handler.OnCancel(session);
            return;
        }

        PendingTaskSnapshot.ClearMeta(session);
    }
}
