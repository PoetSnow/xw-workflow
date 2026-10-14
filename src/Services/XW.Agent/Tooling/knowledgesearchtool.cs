using System.Diagnostics;
using XW.Agent.Application.Abstractions;
using XW.Agent.Application.Models;
using XW.Agent.Application.Options;
using XW.Agent.Contracts.Knowledge;
using Microsoft.Extensions.Options;

namespace XW.Agent.Application.Tooling;

/// <summary>知识库检索。资料集编号只取会话里调用方写入的值。</summary>
public sealed class KnowledgeSearchTool(
    IKnowledgeRetriever retriever,
    IOptionsMonitor<AgentOptions> options) : IAgentTool
{
    /// <summary>工具编码。</summary>
    public string Name => "knowledge.search";

    /// <summary>给模型看的说明。</summary>
    public string Description => "从企业知识库检索与用户问题相关的文档片段";

    /// <summary>所需权限。</summary>
    public string RequiredPermission => "agent.knowledge.read";

    /// <summary>必填槽位。</summary>
    public IReadOnlyList<string> RequiredSlots => ["query"];

    /// <inheritdoc />
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
        var corpusIds = SessionCorpus.Read(session);
        if (corpusIds.Count == 0)
        {
            sw.Stop();
            return new ToolExecutionResult
            {
                ToolName = Name,
                Success = false,
                ElapsedMs = sw.ElapsedMilliseconds,
                Summary = "会话没有资料集编号，知识检索已拒绝。",
                Data = new { query }
            };
        }

        var topK = options.CurrentValue.Knowledge.TopK;
        var result = await retriever.SearchAsync(new KnowledgeSearchRequest
        {
            Query = query,
            CorpusIds = corpusIds,
            TopK = topK
        }, ct);
        sw.Stop();

        if (result.Rejected)
        {
            return new ToolExecutionResult
            {
                ToolName = Name,
                Success = false,
                ElapsedMs = sw.ElapsedMilliseconds,
                Summary = result.Reason ?? "缺少资料集编号。",
                Data = new { query }
            };
        }

        if (result.Hits.Count == 0)
        {
            return new ToolExecutionResult
            {
                ToolName = Name,
                Success = true,
                ElapsedMs = sw.ElapsedMilliseconds,
                Summary = "知识库未命中。请如实告知用户未检索到相关说明，不要编造。",
                Data = new { query, hits = result.Hits }
            };
        }

        return new ToolExecutionResult
        {
            ToolName = Name,
            Success = true,
            ElapsedMs = sw.ElapsedMilliseconds,
            Summary = FormatHits(result.Hits),
            Data = new { query, hits = result.Hits }
        };
    }

    private static string FormatHits(IReadOnlyList<KnowledgeHit> hits) =>
        string.Join("\n---\n", hits.Select((hit, index) =>
            $"#{index + 1} [{hit.CorpusId}] {hit.Title} (doc={hit.DocumentId}, chunk={hit.ChunkId}, score={hit.Score:0.##}, source={hit.Source})\n{hit.Snippet}"));
}
