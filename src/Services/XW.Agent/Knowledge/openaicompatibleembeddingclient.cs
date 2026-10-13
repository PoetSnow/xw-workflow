using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XW.Agent.Application.Options;

namespace XW.Agent.Application.Knowledge;

/// <summary>OpenAI 兼容 Embedding HTTP 客户端。</summary>
public interface IEmbeddingClient
{
    /// <summary>单文本向量化。</summary>
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);

    /// <summary>批量向量化（入库切块用）。</summary>
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
}

/// <summary>OpenAI 兼容 /v1/embeddings；入库与查询必须同一 Model。</summary>
public sealed class OpenAiCompatibleEmbeddingClient(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<AgentOptions> options,
    ILogger<OpenAiCompatibleEmbeddingClient> logger) : IEmbeddingClient
{
    /// <summary>命名 HttpClient。</summary>
    public const string HttpClientName = "xw-agent-embedding";

    /// <inheritdoc />
    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var batch = await EmbedBatchAsync([text], ct);
        return batch[0];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct = default)
    {
        if (texts.Count == 0)
        {
            return [];
        }

        var cfg = options.CurrentValue.Knowledge.Embedding;
        if (!cfg.Enabled)
        {
            throw new InvalidOperationException("Agent:Knowledge:Embedding:Enabled=false");
        }

        if (string.IsNullOrWhiteSpace(cfg.Endpoint) || string.IsNullOrWhiteSpace(cfg.ModelName))
        {
            throw new InvalidOperationException(
                "请配置 Agent:Knowledge:Embedding:Endpoint 与 ModelName（OpenAI 兼容 Embeddings）。");
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var req = new HttpRequestMessage(
            HttpMethod.Post,
            Combine(cfg.Endpoint, cfg.EmbeddingsPath));

        if (!string.IsNullOrWhiteSpace(cfg.ApiKey))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cfg.ApiKey);
        }

        object input = texts.Count == 1 ? texts[0] : texts.ToArray();
        req.Content = JsonContent.Create(new
        {
            model = cfg.ModelName,
            input
        });

        using var resp = await client.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("Embedding HTTP {Code}: {Body}", (int)resp.StatusCode, Truncate(body, 400));
            throw new InvalidOperationException($"Embedding 调用失败 HTTP {(int)resp.StatusCode}");
        }

        var parsed = System.Text.Json.JsonSerializer.Deserialize<EmbeddingResponse>(body)
                     ?? throw new InvalidOperationException("Embedding 响应无法解析");

        var vectors = parsed.Data
            .OrderBy(x => x.Index)
            .Select(x => x.Embedding ?? [])
            .ToList();

        if (vectors.Count != texts.Count)
        {
            throw new InvalidOperationException(
                $"Embedding 条数不匹配 expect={texts.Count} got={vectors.Count}");
        }

        var dim = cfg.Dimensions;
        foreach (var v in vectors)
        {
            if (dim > 0 && v.Length != dim)
            {
                throw new InvalidOperationException(
                    $"Embedding 维数不匹配 expect={dim} got={v.Length}；请对齐模型与 agent_knowledge_chunk.embedding。");
            }
        }

        return vectors;
    }

    private static string Combine(string endpoint, string path)
    {
        var root = endpoint.TrimEnd('/');
        var p = path.StartsWith('/') ? path : "/" + path;
        return root + p;
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? string.Empty : s.Length <= max ? s : s[..max] + "...";

    private sealed class EmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<EmbeddingItem> Data { get; set; } = [];
    }

    private sealed class EmbeddingItem
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }
}
