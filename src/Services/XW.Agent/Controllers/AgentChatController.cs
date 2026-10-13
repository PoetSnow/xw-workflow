using Microsoft.AspNetCore.Mvc;
using XW.Agent.Application.Abstractions;
using XW.Agent.Application.Context;
using XW.Agent.Application.Contracts;
using XW.Agent.Application.Identity;
using XW.Agent.Application.Options;
using XW.Agent.Application.Runtime;
using XW.Agent.Application.Services;
using Microsoft.Extensions.Options;

namespace XW.Agent.Application.Controllers;

/// <summary>对话与会话审计 HTTP API（Identity → Context → Orchestrator）。</summary>
[ApiController]
[Route("api/agent")]
public sealed class AgentChatController(
    IAgentOrchestrator orchestrator,
    IAgentAuditStore audit,
    IAgentTurnStore turns,
    IIdentityResolver identity,
    IUserContextAccessor userContext,
    IOptionsMonitor<AgentOptions> options) : ControllerBase
{
    /// <summary>多轮对话入口（Identity → Context → Runtime）。</summary>
    [HttpPost("chat")]
    public async Task<ActionResult<AgentChatResponse>> Chat([FromBody] AgentChatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { message = "Message is required" });
        }

        var traceId = Guid.NewGuid().ToString("N");
        var ctx = identity.Resolve(
            HttpContext,
            request.UserId,
            request.TenantId,
            request.Roles,
            request.PermissionCodes,
            request.AccessToken,
            request.Confirmed,
            options.CurrentValue.Channel,
            traceId);

        userContext.Current = ctx;
        // 截止时间只包住这一轮。上传接口用 Knowledge.UploadDeadlineSeconds，互不影响。
        using var deadline = RequestDeadline.Start(ct, options.CurrentValue.Runtime.ChatDeadlineSeconds);
        try
        {
            var result = await orchestrator.ChatAsync(request, deadline.Token);
            return Ok(result);
        }
        catch (OperationCanceledException) when (deadline.Fired)
        {
            return Ok(AgentTurnFailures.Deadline(request, traceId));
        }
        catch (SessionConcurrencyException ex)
        {
            return Ok(AgentTurnFailures.SessionConflict(request, traceId, ex.ConflictKey));
        }
        finally
        {
            userContext.Current = null;
        }
    }

    /// <summary>按会话查看审计。</summary>
    [HttpGet("sessions/{sessionId}/audit")]
    public async Task<IActionResult> Audit(string sessionId, CancellationToken ct)
    {
        var rows = await audit.ListBySessionAsync(sessionId, ct);
        return Ok(rows);
    }

    /// <summary>
    /// 按会话查看轮次回放。字段与审计分开，避免旧客户端把 analysis 嵌进审计对象。
    /// </summary>
    [HttpGet("sessions/{sessionId}/turns")]
    public async Task<IActionResult> Turns(string sessionId, CancellationToken ct)
    {
        var rows = await turns.ListBySessionAsync(sessionId, ct);
        return Ok(rows.Select(row => new
        {
            traceId = row.TraceId,
            sessionId = row.SessionId,
            lane = row.Lane,
            laneReason = row.LaneReason,
            phase = row.Phase,
            analysis = ParseAnalysis(row.AnalysisJson),
            createdAt = row.CreatedAt
        }));
    }

    private static System.Text.Json.JsonElement ParseAnalysis(string? json)
    {
        var payload = string.IsNullOrWhiteSpace(json) ? "{}" : json;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(payload);
            return doc.RootElement.Clone();
        }
        catch (System.Text.Json.JsonException)
        {
            using var doc = System.Text.Json.JsonDocument.Parse("{}");
            return doc.RootElement.Clone();
        }
    }
}
