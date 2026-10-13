using System.Diagnostics;
using XW.Agent.Application.Abstractions;
using XW.Agent.Application.Context;
using XW.Agent.Application.Knowledge;
using XW.Agent.Application.Models;
using XW.Agent.Application.Options;
using Microsoft.Extensions.Options;

namespace XW.Agent.Application.Tooling;

/// <summary>知识库检索 Skill。文档在 PG；本 Tool 是唯一检索入口。</summary>
public sealed class KnowledgeSearchTool(
    IKnowledgeRetriever retriever,
    IUserContextAccessor userContext,
    IOptionsMonitor<AgentOptions> options) : IAgentTool
{
    public string Name => "knowledge.search";
    public string Description => "从企业知识库检索与用户问题相关的文档片段";
    public string RequiredPermission => "agent.knowledge.read";
    public IReadOnlyList<string> RequiredSlots => ["query"];

    public async Task<ToolExecutionResult> ExecuteAsync(
        AgentSession session,
        IntentResult intent,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var query = intent.Slots.GetValueOrDefault("query")
                    ?? session.Slots.GetValueOrDefault("query")
                    ?? session.Messages.LastOrDefault(m => m.Role == "user")?.Content
                    ?? string.Empty;

        var user = userContext.Current ?? new UserContext();
        var topK = options.CurrentValue.Knowledge.TopK;
        var hits = await retriever.SearchAsync(query, user, topK, ct);
        sw.Stop();

        if (hits.Count == 0)
        {
            return new ToolExecutionResult
            {
                ToolName = Name,
                Success = true,
                ElapsedMs = sw.ElapsedMilliseconds,
                Summary = "知识库未命中。请如实告知用户未检索到相关说明，不要编造。",
                Data = new { query, hits }
            };
        }

        return new ToolExecutionResult
        {
            ToolName = Name,
            Success = true,
            ElapsedMs = sw.ElapsedMilliseconds,
            Summary = KnowledgeHitFormatter.FormatForPrompt(hits),
            Data = new { query, hits }
        };
    }
}
