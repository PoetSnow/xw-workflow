namespace XW.Agent.Application.Models;

/// <summary>多轮对话会话：槽位、消息历史与元数据。</summary>
public sealed class AgentSession
{
    /// <summary>会话唯一 Id。</summary>
    public required string Id { get; init; }

    /// <summary>调用方用户 Id（可空）。</summary>
    public string? UserId { get; set; }

    /// <summary>租户 Id。随会话落库，供多实例对照；不参与权限判断。</summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// 乐观锁版本。0 表示尚未入库；读出后保存必须带上本次读到的版本。
    /// </summary>
    public int Version { get; set; }

    /// <summary>创建时间（UTC）。</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>最近更新时间（UTC）。</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>槽位字典（keywords、productId、_confirmed 等）。</summary>
    public Dictionary<string, string> Slots { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>对话消息列表。</summary>
    public List<AgentMessage> Messages { get; init; } = [];

    /// <summary>
    /// 本次加载时的槽位副本。冲突合并只提交相对它的改动，避免无声盖掉另一实例写入的键。不入库。
    /// </summary>
    public Dictionary<string, string> SlotBaseline { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>本次加载时的消息条数。保存冲突时只追加这条数之后的新消息。不入库。</summary>
    public int MessageBaseline { get; set; }

    /// <summary>把当前槽位和消息条数记成基线。每次读出和成功保存后调用。</summary>
    public void CaptureBaseline()
    {
        SlotBaseline.Clear();
        foreach (var (key, value) in Slots)
        {
            SlotBaseline[key] = value;
        }

        MessageBaseline = Messages.Count;
    }
}

/// <summary>单条会话消息。</summary>
public sealed class AgentMessage
{
    /// <summary>角色：user | assistant | system。</summary>
    public required string Role { get; init; }

    /// <summary>消息正文。</summary>
    public required string Content { get; init; }

    /// <summary>关联意图码（可选）。</summary>
    public string? Intent { get; init; }

    /// <summary>消息时间（UTC）。</summary>
    public DateTimeOffset At { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>意图识别结果：Intent、置信度、槽位与是否需追问。</summary>
public sealed class IntentResult
{
    /// <summary>意图码（如 inventory.query / agent_plan / unknown）。</summary>
    public required string Intent { get; init; }

    /// <summary>置信度 0~1。</summary>
    public double Confidence { get; init; }

    /// <summary>抽取到的槽位。</summary>
    public Dictionary<string, string> Slots { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>缺槽或 LLM 显式要求追问。</summary>
    public bool NeedsClarification => MissingSlots.Count > 0 || ForceClarification;

    /// <summary>强制追问（即使无 MissingSlots）。</summary>
    public bool ForceClarification { get; init; }

    /// <summary>缺失的必填槽位名。</summary>
    public List<string> MissingSlots { get; init; } = [];

    /// <summary>给回复层的提示文案。</summary>
    public string? ReplyHint { get; init; }
}

/// <summary>单次工具执行结果。</summary>
public sealed class ToolExecutionResult
{
    /// <summary>工具名（Capability.Code）。</summary>
    public required string ToolName { get; init; }

    /// <summary>是否成功（Stub 假成功也会为 true，需结合 Summary 判断）。</summary>
    public bool Success { get; init; }

    /// <summary>给人/Planner 看的摘要。</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>结构化数据（HTTP body 包装或 Stub 对象）。</summary>
    public object? Data { get; init; }

    /// <summary>耗时毫秒。</summary>
    public long ElapsedMs { get; init; }

    /// <summary>失败原因（如 HTTP 401）。</summary>
    public string? Error { get; init; }

    /// <summary>下游 HTTP 状态码；鉴权失败时为 401/403。</summary>
    public int? HttpStatusCode { get; init; }
}

/// <summary>一轮对话的审计快照。</summary>
public sealed class AgentAuditRecord
{
    /// <summary>链路追踪 Id。</summary>
    public required string TraceId { get; init; }

    /// <summary>会话 Id。</summary>
    public required string SessionId { get; init; }

    /// <summary>用户 Id。</summary>
    public string? UserId { get; init; }

    /// <summary>租户 Id。</summary>
    public string? TenantId { get; init; }

    /// <summary>识别/规划意图。</summary>
    public string? Intent { get; init; }

    /// <summary>用户原文。</summary>
    public required string UserMessage { get; init; }

    /// <summary>助手最终回复。</summary>
    public required string AssistantReply { get; init; }

    /// <summary>本轮工具调用明细。</summary>
    public List<ToolExecutionResult> ToolCalls { get; init; } = [];

    /// <summary>是否因权限拒绝。</summary>
    public bool PermissionDenied { get; init; }

    /// <summary>是否需要用户确认后继续。</summary>
    public bool ApprovalRequired { get; init; }

    /// <summary>记录时间（UTC）。</summary>
    public DateTimeOffset At { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 一轮对话的回放记录。回复生成失败、规划抛错或截止时间触发时也要留下 lane 和已发生的步骤。
/// 不含用户原文，避免和审计表重复。
/// </summary>
public sealed class AgentTurnRecord
{
    /// <summary>与本轮响应 traceId 相同。</summary>
    public required string TraceId { get; init; }

    /// <summary>会话 Id。</summary>
    public required string SessionId { get; init; }

    /// <summary>车道，如 Action / Chitchat。</summary>
    public string? Lane { get; init; }

    /// <summary>车道路由原因。</summary>
    public string? LaneReason { get; init; }

    /// <summary>结束相位，如 tool_ok、denied、deadline、error。</summary>
    public string? Phase { get; init; }

    /// <summary>分析 JSON（步骤、耗时、Token），列名 analysis。</summary>
    public string AnalysisJson { get; init; } = "{}";

    /// <summary>写入时间（UTC）。</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>内置意图常量（旧规则路由兼容；主路径以 Capability IntentCode 为准）。</summary>
public static class AgentIntents
{
    public const string Unknown = "unknown";
    public const string KnowledgeAnswer = "knowledge.answer";
    public const string AgentPlan = "agent_plan";
    public const string Chitchat = "chitchat";
    public const string QueryInventory = "query_inventory";
    public const string QueryOrderFulfillment = "query_order_fulfillment";
    public const string ListLowStockAlerts = "list_low_stock_alerts";
}

/// <summary>Agent 侧能力权限码常量。</summary>
public static class AgentPermissionCodes
{
    public const string InventoryRead = "agent.inventory.read";
    public const string OrderRead = "agent.order.read";
    public const string AlertRead = "agent.alert.read";
    public const string KnowledgeRead = "agent.knowledge.read";
}
