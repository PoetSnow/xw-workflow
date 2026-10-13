namespace XW.Agent.Application.Contracts;

/// <summary>
/// 一轮对话没跑完时仍返回 <see cref="AgentChatResponse"/>，不改必填字段。
/// 前端按 phase 区分：deadline = 服务端截止；conflict = 会话版本冲突。
/// </summary>
public static class AgentTurnFailures
{
    /// <summary>聊天截止时间到了。停止后续工具调用，不使用 408，避免调用方把成功体解析失败。</summary>
    public static AgentChatResponse Deadline(AgentChatRequest request, string traceId) =>
        Build(
            request,
            traceId,
            "本轮处理超过时间限制，已停止后续调用。请缩小问题范围或稍后重试。",
            phase: "deadline",
            laneReason: "chat_deadline");

    /// <summary>
    /// 同一会话的另一轮改了同一个槽位。本轮不覆盖那次写入。
    /// </summary>
    public static AgentChatResponse SessionConflict(AgentChatRequest request, string traceId, string? conflictKey)
    {
        var key = string.IsNullOrWhiteSpace(conflictKey) ? "会话版本" : conflictKey;
        return Build(
            request,
            traceId,
            $"会话已被另一轮更新（冲突字段 {key}），这次没有覆盖。请重试。",
            phase: "conflict",
            laneReason: "session_version");
    }

    private static AgentChatResponse Build(
        AgentChatRequest request,
        string traceId,
        string reply,
        string phase,
        string laneReason) =>
        new(
            request.SessionId ?? string.Empty,
            reply,
            null,
            null,
            false,
            false,
            [],
            [],
            traceId,
            request.TenantId,
            request.UserId,
            new AgentTurnAnalysisDto(0, null, laneReason, phase, 0, 0, 0, [], []));
}
