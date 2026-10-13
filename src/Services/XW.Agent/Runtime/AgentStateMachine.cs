namespace XW.Agent.Application.Runtime;

/// <summary>
/// Agent 运行状态机（单轮对话）。
/// Retrieve → Plan → Authorize → Execute → Observe → Plan …
/// 旁路：Clarify / Confirm / Deny / Reply。
/// </summary>
public enum AgentRunState
{
    /// <summary>初始。</summary>
    Idle = 0,
    /// <summary>检索候选能力 / 知识。</summary>
    Retrieve = 1,
    /// <summary>LLM 规划下一步。</summary>
    Plan = 2,
    /// <summary>权限与确认校验。</summary>
    Authorize = 3,
    /// <summary>执行 Tool / Workflow。</summary>
    Execute = 4,
    /// <summary>写入观察结果。</summary>
    Observe = 5,
    /// <summary>需要用户澄清。</summary>
    Clarify = 6,
    /// <summary>需要用户确认。</summary>
    Confirm = 7,
    /// <summary>权限拒绝。</summary>
    Deny = 8,
    /// <summary>生成最终回复。</summary>
    Reply = 9,
    /// <summary>结束。</summary>
    Done = 10
}

/// <summary>运行状态机：校验合法转移并记录历史（调试/遥测用）。</summary>
public sealed class AgentStateMachine
{
    /// <summary>当前状态。</summary>
    public AgentRunState State { get; private set; } = AgentRunState.Idle;

    /// <summary>转移历史。</summary>
    public IReadOnlyList<(AgentRunState From, AgentRunState To, string? Note)> History => _history;
    private readonly List<(AgentRunState From, AgentRunState To, string? Note)> _history = [];

    /// <summary>合法转移；非法则抛异常。</summary>
    public void Transition(AgentRunState to, string? note = null)
    {
        if (!IsAllowed(State, to))
        {
            throw new InvalidOperationException($"非法状态转移 {State} → {to}");
        }

        _history.Add((State, to, note));
        State = to;
    }

    /// <summary>判断 from→to 是否允许。</summary>
    public static bool IsAllowed(AgentRunState from, AgentRunState to) => (from, to) switch
    {
        (AgentRunState.Idle, AgentRunState.Retrieve) => true,
        (AgentRunState.Idle, AgentRunState.Plan) => true, // Retrieval 关闭时直达 Plan
        (AgentRunState.Retrieve, AgentRunState.Plan) => true,
        (AgentRunState.Retrieve, AgentRunState.Deny) => true,
        (AgentRunState.Retrieve, AgentRunState.Clarify) => true,
        (AgentRunState.Plan, AgentRunState.Authorize) => true,
        (AgentRunState.Plan, AgentRunState.Clarify) => true,
        (AgentRunState.Plan, AgentRunState.Confirm) => true,
        (AgentRunState.Plan, AgentRunState.Reply) => true,
        (AgentRunState.Plan, AgentRunState.Retrieve) => true, // 下一步前可再检索
        (AgentRunState.Authorize, AgentRunState.Execute) => true,
        (AgentRunState.Authorize, AgentRunState.Deny) => true,
        (AgentRunState.Authorize, AgentRunState.Confirm) => true,
        (AgentRunState.Execute, AgentRunState.Observe) => true,
        // Evaluator 裁定失败/空结果后可直接收束到 Reply（或经 Clarify）
        (AgentRunState.Execute, AgentRunState.Reply) => true,
        (AgentRunState.Execute, AgentRunState.Clarify) => true,
        (AgentRunState.Observe, AgentRunState.Retrieve) => true,
        (AgentRunState.Observe, AgentRunState.Plan) => true,
        (AgentRunState.Observe, AgentRunState.Reply) => true,
        (AgentRunState.Observe, AgentRunState.Clarify) => true,
        (AgentRunState.Clarify, AgentRunState.Reply) => true,
        (AgentRunState.Confirm, AgentRunState.Reply) => true,
        (AgentRunState.Deny, AgentRunState.Reply) => true,
        (AgentRunState.Reply, AgentRunState.Done) => true,
        _ => false
    };
}
