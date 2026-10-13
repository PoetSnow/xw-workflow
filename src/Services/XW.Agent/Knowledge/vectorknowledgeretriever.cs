using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XW.Agent.Application.Context;
using XW.Agent.Application.Options;
using XW.Agent.Application.Telemetry;

namespace XW.Agent.Application.Knowledge;

/// <summary>生产检索：问句 Embedding → pgvector。关开关则空结果，绝不回退词库。</summary>
public sealed class VectorKnowledgeRetriever(
    IEmbeddingClient embedding,
    IKnowledgeVectorStore store,
    IOptionsMonitor<AgentOptions> options,
    ILogger<VectorKnowledgeRetriever> logger) : IKnowledgeRetriever
{
    public async Task<IReadOnlyList<KnowledgeHit>> SearchAsync(
        string query,
        UserContext user,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        using var activity = AgentTelemetry.StartState("rag.vector");
        var cfg = options.CurrentValue.Knowledge;

        if (!cfg.Enabled)
        {
            activity?.SetTag("agent.rag.enabled", false);
            activity?.SetTag("agent.rag.skip", "knowledge_disabled");
            AgentTelemetry.SetOk(activity, true);
            return [];
        }

        if (!cfg.Embedding.Enabled)
        {
            activity?.SetTag("agent.rag.enabled", true);
            activity?.SetTag("agent.rag.skip", "embedding_disabled");
            logger.LogInformation("RAG skipped: Knowledge.Embedding.Enabled=false（不回退词法）");
            AgentTelemetry.SetOk(activity, true);
            return [];
        }

        var q = query?.Trim() ?? string.Empty;
        if (q.Length == 0)
        {
            return [];
        }

        var k = topK > 0 ? topK : Math.Max(1, cfg.TopK);
        var vector = await embedding.EmbedAsync(q, cancellationToken);
        var hits = await store.SearchAsync(
            vector,
            k,
            domain: null,
            cfg.MinSimilarity,
            cancellationToken);

        activity?.SetTag("agent.rag.hit_count", hits.Count);
        activity?.SetTag("agent.rag.provider", "vector");
        AgentTelemetry.SetOk(activity, true);

        logger.LogInformation(
            "Vector RAG hits={Hits} top=[{Top}] queryLen={Len}",
            hits.Count,
            string.Join(",", hits.Take(3).Select(h => $"{h.Id}:{h.Score:0.##}")),
            q.Length);

        return hits;
    }
}

/// <summary>
/// 知识检索入口：仅 vector；未开启则空结果。
/// 不再提供 lexical 回退（避免「关向量又走词库」）。
/// </summary>
public sealed class ConfigurableKnowledgeRetriever(
    IOptionsMonitor<AgentOptions> options,
    VectorKnowledgeRetriever vector,
    ILogger<ConfigurableKnowledgeRetriever> logger) : IKnowledgeRetriever
{
    public Task<IReadOnlyList<KnowledgeHit>> SearchAsync(
        string query,
        UserContext user,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        var cfg = options.CurrentValue.Knowledge;
        if (!cfg.Enabled)
        {
            logger.LogDebug("Knowledge.Enabled=false → skip RAG");
            return Task.FromResult<IReadOnlyList<KnowledgeHit>>([]);
        }

        var provider = cfg.Provider?.Trim() ?? "vector";
        if (!string.Equals(provider, "vector", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Knowledge.Provider={Provider} 已忽略：仅支持 vector；不回退词法，本次跳过检索",
                provider);
            return Task.FromResult<IReadOnlyList<KnowledgeHit>>([]);
        }

        return vector.SearchAsync(query, user, topK, cancellationToken);
    }
}
