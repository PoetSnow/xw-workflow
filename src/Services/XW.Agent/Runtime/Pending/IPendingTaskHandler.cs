using XW.Agent.Application.Models;
using XW.Agent.Application.Options;
using XW.Agent.Application.Planning;

namespace XW.Agent.Application.Runtime.Pending;

/// <summary>
/// 可插拔待办处理器：采购/销售/入库等写流程各自实现，Orchestrator 只分发。
/// </summary>
public interface IPendingTaskHandler
{
    /// <summary>待办 kind，如 purchase_create。</summary>
    string Kind { get; }

    /// <summary>取消时清理本业务槽位。</summary>
    void OnCancel(AgentSession session);

    /// <summary>本轮推进：产出下一步 call_tool / clarify（observations 为空时）。</summary>
    bool TryPlan(
        AgentSession session,
        PendingTaskReplyKind reply,
        IReadOnlyList<AgentCapabilityDefinition> allowedCaps,
        out AgentPlanDecision decision);

    /// <summary>工具成功后：比价结果绑定、二次确认、或继续 create。</summary>
    bool TryAfterTool(
        AgentSession session,
        IReadOnlyList<AgentCapabilityDefinition> allowedCaps,
        IReadOnlyList<AgentPlanObservation> observations,
        IReadOnlyList<string> summaries,
        out AgentPlanDecision decision);

    /// <summary>任意工具成功后的副作用（绑定数据、完成后清待办）。</summary>
    void OnToolSuccess(AgentSession session, string toolCode, object? data);
}
