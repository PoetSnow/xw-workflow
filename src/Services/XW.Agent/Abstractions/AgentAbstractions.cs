using XW.Agent.Application.Models;

namespace XW.Agent.Application.Abstractions;

/// <summary>会话持久化：按 SessionId 获取/创建与保存多轮对话状态。</summary>
public interface IAgentSessionStore
{
    /// <summary>按会话 Id 取出会话；为空则新建。</summary>
    Task<AgentSession> GetOrCreateAsync(string? sessionId, string? userId, CancellationToken ct = default);

    /// <summary>保存槽位与消息历史。</summary>
    Task SaveAsync(AgentSession session, CancellationToken ct = default);
}

/// <summary>意图路由：将用户自然语言映射为 Intent + Slots（LLM 主路径，规则兜底）。</summary>
public interface IIntentRouter
{
    /// <summary>对当前用户消息做意图识别。</summary>
    Task<IntentResult> RouteAsync(AgentSession session, string message, CancellationToken ct = default);
}

/// <summary>可执行工具（HTTP Capability / Stub / 知识检索等）的统一契约。</summary>
public interface IAgentTool
{
    /// <summary>工具编码，通常等于 Capability.Code。</summary>
    string Name { get; }

    /// <summary>给人看的简短说明。</summary>
    string Description { get; }

    /// <summary>执行所需的权限码（单权限兼容字段）。</summary>
    string RequiredPermission { get; }

    /// <summary>必填槽位名列表。</summary>
    IReadOnlyList<string> RequiredSlots { get; }

    /// <summary>执行工具并返回结构化结果（成功/失败摘要与数据）。</summary>
    Task<ToolExecutionResult> ExecuteAsync(AgentSession session, IntentResult intent, CancellationToken ct = default);
}

/// <summary>工具注册表：按意图或 Code 查找可执行 Tool。</summary>
public interface IToolRegistry
{
    /// <summary>按意图码查找（兼容旧 Intent→Tool 路径）。</summary>
    IAgentTool? FindByIntent(string intent);

    /// <summary>按 Capability/Tool Code 查找。</summary>
    IAgentTool? FindByCode(string code);

    /// <summary>全部已注册工具。</summary>
    IReadOnlyList<IAgentTool> All { get; }
}

/// <summary>审计存储：记录每轮对话的工具调用与权限决策。</summary>
public interface IAgentAuditStore
{
    /// <summary>追加一条审计记录。</summary>
    Task AppendAsync(AgentAuditRecord record, CancellationToken ct = default);

    /// <summary>按会话列出审计记录。</summary>
    Task<IReadOnlyList<AgentAuditRecord>> ListBySessionAsync(string sessionId, CancellationToken ct = default);
}

/// <summary>
/// 轮次回放。与审计分开：审计在回复成功后写，轮次在 ChatAsync 的 finally 里写，失败也留痕。
/// </summary>
public interface IAgentTurnStore
{
    /// <summary>追加一轮。调用方在取消后仍应传入未取消的 Token，避免截止时间把插入本身掐掉。</summary>
    Task AppendAsync(AgentTurnRecord record, CancellationToken ct = default);

    /// <summary>按会话列出轮次，时间升序。</summary>
    Task<IReadOnlyList<AgentTurnRecord>> ListBySessionAsync(string sessionId, CancellationToken ct = default);
}

/// <summary>对话编排入口：Identity/Context 之后的 Runtime 主循环。</summary>
public interface IAgentOrchestrator
{
    /// <summary>处理一轮用户消息并返回助手回复（含工具调用日志）。</summary>
    Task<Contracts.AgentChatResponse> ChatAsync(Contracts.AgentChatRequest request, CancellationToken ct = default);
}
