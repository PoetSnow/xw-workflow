namespace XW.Agent.Application.Contracts;

/// <summary>创建或继续会话的聊天请求体。</summary>
/// <param name="SessionId">已有会话 Id；空则新建。</param>
/// <param name="Message">用户本轮消息。</param>
/// <param name="UserId">显式用户 Id（可覆盖 Token 解析）。</param>
/// <param name="TenantId">租户 Id。</param>
/// <param name="Roles">角色列表，用于 Policy 映射权限。</param>
/// <param name="PermissionCodes">显式权限码（可含 *）。</param>
/// <param name="AccessToken">调用方 Token，forward_caller 下游鉴权用。</param>
/// <param name="Confirmed">高风险操作二次确认标记。</param>
/// <param name="CorpusIds">调用方写入会话的资料集编号。模型不能改。</param>
public sealed record AgentChatRequest(
    string? SessionId,
    string Message,
    string? UserId,
    string? TenantId,
    IReadOnlyList<string>? Roles,
    IReadOnlyList<string>? PermissionCodes,
    string? AccessToken,
    bool Confirmed = false,
    IReadOnlyList<string>? CorpusIds = null);

/// <summary>一轮对话结果。</summary>
/// <param name="SessionId">会话 Id。</param>
/// <param name="Reply">面向用户的自然语言回复。</param>
/// <param name="Intent">意图/规划相位码。</param>
/// <param name="IntentConfidence">意图置信度。</param>
/// <param name="NeedsClarification">是否需要用户补充信息。</param>
/// <param name="NeedsApproval">是否需要用户确认后继续。</param>
/// <param name="MissingSlots">缺失槽位名。</param>
/// <param name="ToolCalls">本轮工具/工作流步骤调用日志。</param>
/// <param name="TraceId">链路追踪 Id。</param>
/// <param name="TenantId">租户 Id。</param>
/// <param name="UserId">用户 Id。</param>
/// <param name="Analysis">本轮分析日志（车道/步骤/耗时/Token）。</param>
public sealed record AgentChatResponse(
    string SessionId,
    string Reply,
    string? Intent,
    double? IntentConfidence,
    bool NeedsClarification,
    bool NeedsApproval,
    IReadOnlyList<string> MissingSlots,
    IReadOnlyList<AgentToolCallLogDto> ToolCalls,
    string TraceId,
    string? TenantId,
    string? UserId,
    AgentTurnAnalysisDto? Analysis = null);

/// <summary>单轮链路分析（前端旁路日志面板）。</summary>
public sealed record AgentTurnAnalysisDto(
    long TotalElapsedMs,
    string? Lane,
    string? LaneReason,
    string? Phase,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    IReadOnlyList<AgentAnalysisStepDto> Steps,
    IReadOnlyList<AgentLlmUsageDto> LlmCalls);

/// <summary>分析步骤。</summary>
public sealed record AgentAnalysisStepDto(
    string Kind,
    string Name,
    long ElapsedMs,
    string? Detail,
    bool? Success);

/// <summary>单次 LLM 调用用量。</summary>
public sealed record AgentLlmUsageDto(
    string CallId,
    string? Profile,
    string? Model,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    long ElapsedMs);

/// <summary>单次工具调用日志（对外 API 精简字段）。</summary>
/// <param name="ToolName">工具/能力码。</param>
/// <param name="Success">是否成功。</param>
/// <param name="Summary">摘要。</param>
/// <param name="ElapsedMs">耗时毫秒。</param>
public sealed record AgentToolCallLogDto(
    string ToolName,
    bool Success,
    string? Summary,
    long ElapsedMs);

/// <summary>会话概要（列表/调试用）。</summary>
/// <param name="SessionId">会话 Id。</param>
/// <param name="UserId">用户 Id。</param>
/// <param name="CreatedAt">创建时间。</param>
/// <param name="UpdatedAt">更新时间。</param>
/// <param name="MessageCount">消息条数。</param>
public sealed record AgentSessionDto(
    string SessionId,
    string? UserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int MessageCount);
