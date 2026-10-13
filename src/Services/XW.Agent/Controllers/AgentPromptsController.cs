using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using XW.Agent.Application.Options;

namespace XW.Agent.Application.Controllers;

/// <summary>查看当前生效的 Prompt（可在 appsettings Agent:Prompts 覆盖）。按车道拆分。</summary>
[ApiController]
[Route("api/agent/prompts")]
public sealed class AgentPromptsController(IOptionsMonitor<AgentOptions> options) : ControllerBase
{
    [HttpGet]
    public ActionResult<object> Get()
    {
        var p = options.CurrentValue.Prompts;
        return Ok(new
        {
            lanes = new
            {
                knowledge = "KnowledgeReply* — RAG 作答，不调 API",
                action = "Reply* + Planner* — 业务编排与 Tool 事实回复",
                intentLegacy = "Intent* — 仅 Legacy 细粒度分类"
            },
            intentSystem = p.IntentSystem,
            intentUserTemplate = p.IntentUserTemplate,
            plannerSystem = p.PlannerSystem,
            plannerUserTemplate = p.PlannerUserTemplate,
            replySystem = p.ReplySystem,
            replyUserTemplate = p.ReplyUserTemplate,
            knowledgeReplySystem = p.KnowledgeReplySystem,
            knowledgeReplyUserTemplate = p.KnowledgeReplyUserTemplate,
            laneRouterSystem = p.LaneRouterSystem,
            laneRouterUserTemplate = p.LaneRouterUserTemplate,
            placeholders = new
            {
                intentUser = new[] { "catalog_json", "prior_slots_json", "history", "message" },
                plannerUser = new[] { "tools_json", "observations", "history", "message", "confirmed" },
                replyUser = new[]
                {
                    "capabilities", "phase", "intent", "confidence", "slots_json",
                    "missing_slots", "tool_summary", "fallback", "history", "message"
                },
                knowledgeReplyUser = new[] { "phase", "knowledge", "fallback", "history", "message" },
                laneRouterUser = new[] { "history", "message" }
            }
        });
    }
}
