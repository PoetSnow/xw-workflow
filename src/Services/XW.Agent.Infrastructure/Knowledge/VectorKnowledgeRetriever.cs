using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XW.Agent.Contracts.Knowledge;

namespace XW.Agent.Infrastructure.Knowledge;

/// <summary>问句向量化后只在给定资料集里检索。关闭向量或 Embedding 时返回空结果。</summary>
public sealed class VectorKnowledgeRetriever(
    IEmbeddingClient embedding,
    IKnowledgeVectorStore store,
    IOptionsMonitor<KnowledgeConnectionOptions> connection,
    IOptionsMonitor<EmbeddingOptions> embeddingOptions,
    ILogger<VectorKnowledgeRetriever> logger) : IKnowledgeRetriever
{
    /// <inheritdoc />
    public async Task<KnowledgeSearchResult> SearchAsync(
        KnowledgeSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var corpusIds = CorpusIds.Normalize(request.CorpusIds);
        if (corpusIds.Count == 0)
        {
            return KnowledgeSearchResult.Reject("缺少资料集编号。");
        }

        var cfg = connection.CurrentValue;
        if (!cfg.Enabled)
        {
            logger.LogDebug("Knowledge.Enabled=false，返回空结果");
            return KnowledgeSearchResult.Found([]);
        }

        if (!string.Equals(cfg.Provider?.Trim(), "vector", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Knowledge.Provider={Provider} 已忽略：仅支持 vector，本次返回空结果",
                cfg.Provider);
            return KnowledgeSearchResult.Found([]);
        }

        if (!embeddingOptions.CurrentValue.Enabled)
        {
            logger.LogInformation("Embedding 已关闭，返回空结果");
            return KnowledgeSearchResult.Found([]);
        }

        var query = request.Query?.Trim() ?? string.Empty;
        if (query.Length == 0)
        {
            return KnowledgeSearchResult.Found([]);
        }

        var topK = request.TopK > 0 ? request.TopK : Math.Max(1, cfg.TopK);
        var vector = await embedding.EmbedAsync(query, cancellationToken);
        var hits = await store.SearchAsync(vector, topK, corpusIds, cfg.MinSimilarity, cancellationToken);
        logger.LogInformation(
            "Vector RAG hits={Hits} corpora={Corpora} queryLen={Len}",
            hits.Count,
            corpusIds.Count,
            query.Length);
        return KnowledgeSearchResult.Found(hits);
    }
}
