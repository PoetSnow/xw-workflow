using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XW.Agent.Application.Context;
using XW.Agent.Application.Options;
using XW.Agent.Application.Telemetry;

namespace XW.Agent.Application.Knowledge;

/// <summary>知识检索器：按查询返回命中片段（向量或历史词法实现）。</summary>
public interface IKnowledgeRetriever
{
    /// <summary>检索 TopK 知识片段。</summary>
    Task<IReadOnlyList<KnowledgeHit>> SearchAsync(
        string query,
        UserContext user,
        int topK = 5,
        CancellationToken cancellationToken = default);
}

/// <summary>进程内/文件知识块只读存储（演示用；生产走 pgvector）。</summary>
public interface IKnowledgeStore
{
    /// <summary>列出全部切块。</summary>
    IReadOnlyList<KnowledgeChunk> ListChunks();

    /// <summary>文档数。</summary>
    int DocumentCount { get; }

    /// <summary>切块数。</summary>
    int ChunkCount { get; }
}

/// <summary>单条知识命中结果。</summary>
public sealed class KnowledgeHit
{
    /// <summary>切块或命中 Id。</summary>
    public required string Id { get; init; }

    /// <summary>文档标题。</summary>
    public required string Title { get; init; }

    /// <summary>摘要片段。</summary>
    public required string Snippet { get; init; }

    /// <summary>业务域标签。</summary>
    public string Domain { get; init; } = string.Empty;

    /// <summary>来源（文件名等）。</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>相似度/词法分。</summary>
    public double Score { get; init; }
}

/// <summary>知识文档（入库前中间模型）。</summary>
public sealed class KnowledgeDocument
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Domain { get; init; }
    public required string Source { get; init; }
    public required string Content { get; init; }
}

/// <summary>知识切块（词法索引单元）。</summary>
public sealed class KnowledgeChunk
{
    public required string Id { get; init; }
    public required string DocumentId { get; init; }
    public required string Title { get; init; }
    public required string Domain { get; init; }
    public required string Source { get; init; }
    public required string Text { get; init; }
}

/// <summary>文件 + 内置种子语料的知识库（MVP，进程内索引）。</summary>
public sealed class FileKnowledgeStore : IKnowledgeStore
{
    private readonly List<KnowledgeChunk> _chunks;
    private readonly int _docCount;

    public FileKnowledgeStore(
        IOptionsMonitor<AgentOptions> options,
        IHostEnvironment env,
        ILogger<FileKnowledgeStore> logger)
    {
        var cfg = options.CurrentValue.Knowledge;
        var docs = new List<KnowledgeDocument>();

        var seedDir = Path.Combine(env.ContentRootPath, cfg.SeedRelativePath);
        if (Directory.Exists(seedDir))
        {
            foreach (var file in Directory.EnumerateFiles(seedDir, "*.md", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var text = File.ReadAllText(file, Encoding.UTF8);
                    docs.Add(ParseMarkdown(Path.GetFileNameWithoutExtension(file), file, text));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Skip knowledge seed file {File}", file);
                }
            }
        }

        if (docs.Count == 0)
        {
            logger.LogInformation("No seed files under {Dir}; loading embedded MVP corpus", seedDir);
            docs.AddRange(EmbeddedSeedCorpus.Documents);
        }

        _docCount = docs.Count;
        _chunks = docs.SelectMany(ChunkDocument).ToList();
        logger.LogInformation(
            "Knowledge store ready docs={Docs} chunks={Chunks} dir={Dir}",
            _docCount, _chunks.Count, seedDir);
    }

    public IReadOnlyList<KnowledgeChunk> ListChunks() => _chunks;
    public int DocumentCount => _docCount;
    public int ChunkCount => _chunks.Count;

    private static KnowledgeDocument ParseMarkdown(string id, string source, string text)
    {
        var title = id;
        var domain = "general";
        var body = text;

        var lines = text.Replace("\r\n", "\n").Split('\n');
        if (lines.Length > 0 && lines[0].StartsWith("# ", StringComparison.Ordinal))
        {
            title = lines[0][2..].Trim();
            body = string.Join("\n", lines.Skip(1)).Trim();
        }

        var domainMatch = Regex.Match(text, @"^domain:\s*(.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        if (domainMatch.Success)
        {
            domain = domainMatch.Groups[1].Value.Trim();
        }

        return new KnowledgeDocument
        {
            Id = id,
            Title = title,
            Domain = domain,
            Source = Path.GetFileName(source),
            Content = body
        };
    }

    private static IEnumerable<KnowledgeChunk> ChunkDocument(KnowledgeDocument doc)
    {
        var parts = Regex.Split(doc.Content, @"\n#{1,3}\s+")
            .Select(p => p.Trim())
            .Where(p => p.Length >= 20)
            .ToList();

        if (parts.Count == 0)
        {
            parts = [doc.Content.Trim()];
        }

        // 过长再按段落切
        var refined = new List<string>();
        foreach (var part in parts)
        {
            if (part.Length <= 800)
            {
                refined.Add(part);
                continue;
            }

            var paras = Regex.Split(part, @"\n\s*\n")
                .Select(p => p.Trim())
                .Where(p => p.Length > 0);
            var buf = new StringBuilder();
            foreach (var para in paras)
            {
                if (buf.Length + para.Length > 700 && buf.Length > 0)
                {
                    refined.Add(buf.ToString());
                    buf.Clear();
                }

                if (buf.Length > 0)
                {
                    buf.Append("\n\n");
                }

                buf.Append(para);
            }

            if (buf.Length > 0)
            {
                refined.Add(buf.ToString());
            }
        }

        for (var i = 0; i < refined.Count; i++)
        {
            yield return new KnowledgeChunk
            {
                Id = $"{doc.Id}#{i + 1}",
                DocumentId = doc.Id,
                Title = doc.Title,
                Domain = doc.Domain,
                Source = doc.Source,
                Text = refined[i]
            };
        }
    }
}

/// <summary>词法 RAG（MVP）：可离线跑；后续可换向量检索实现同一接口。</summary>
public sealed class LexicalKnowledgeRetriever(
    IKnowledgeStore store,
    IOptionsMonitor<AgentOptions> options,
    ILogger<LexicalKnowledgeRetriever> logger) : IKnowledgeRetriever
{
    private static readonly Regex TokenSplit = new(@"[\s,，。；;、/\-_|？?！!：:（）()【】\[\]]+", RegexOptions.Compiled);

    public Task<IReadOnlyList<KnowledgeHit>> SearchAsync(
        string query,
        UserContext user,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        using var activity = AgentTelemetry.StartState("rag");
        var cfg = options.CurrentValue.Knowledge;
        if (!cfg.Enabled)
        {
            activity?.SetTag("agent.rag.enabled", false);
            return Task.FromResult<IReadOnlyList<KnowledgeHit>>([]);
        }

        var k = topK > 0 ? topK : Math.Max(1, cfg.TopK);
        var q = query?.Trim() ?? string.Empty;
        if (q.Length == 0)
        {
            return Task.FromResult<IReadOnlyList<KnowledgeHit>>([]);
        }

        var tokens = Tokenize(q);
        var hits = store.ListChunks()
            .Select(c =>
            {
                var score = Score(c, q, tokens);
                return (Chunk: c, Score: score);
            })
            .Where(x => x.Score >= cfg.MinScore)
            .OrderByDescending(x => x.Score)
            .Take(k)
            .Select(x => new KnowledgeHit
            {
                Id = x.Chunk.Id,
                Title = x.Chunk.Title,
                Domain = x.Chunk.Domain,
                Source = x.Chunk.Source,
                Snippet = Truncate(x.Chunk.Text, 900),
                Score = Math.Round(x.Score, 3)
            })
            .ToList();

        activity?.SetTag("agent.rag.hit_count", hits.Count);
        activity?.SetTag("agent.rag.query_len", q.Length);
        AgentTelemetry.SetOk(activity, true);

        logger.LogInformation(
            "RAG search hits={Hits} top=[{Top}] queryLen={Len}",
            hits.Count,
            string.Join(",", hits.Take(3).Select(h => $"{h.Id}:{h.Score:0.##}")),
            q.Length);

        return Task.FromResult<IReadOnlyList<KnowledgeHit>>(hits);
    }

    private static double Score(KnowledgeChunk chunk, string query, HashSet<string> tokens)
    {
        double score = 0;
        var text = chunk.Text;
        var title = chunk.Title;

        if (query.Length >= 2 && text.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            score += 5;
        }

        if (query.Length >= 2 && title.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            score += 3;
        }

        foreach (var t in tokens)
        {
            if (t.Length < 2)
            {
                continue;
            }

            if (title.Contains(t, StringComparison.OrdinalIgnoreCase))
            {
                score += 1.5;
            }

            if (text.Contains(t, StringComparison.OrdinalIgnoreCase))
            {
                score += 1.0;
            }

            if (chunk.Domain.Contains(t, StringComparison.OrdinalIgnoreCase))
            {
                score += 0.8;
            }
        }

        // 中文 2-gram
        for (var i = 0; i + 1 < query.Length; i++)
        {
            var gram = query.Substring(i, 2);
            if (char.IsWhiteSpace(gram[0]) || char.IsWhiteSpace(gram[1]))
            {
                continue;
            }

            if (text.Contains(gram, StringComparison.OrdinalIgnoreCase))
            {
                score += 0.35;
            }
        }

        return score;
    }

    private static HashSet<string> Tokenize(string query)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in TokenSplit.Split(query))
        {
            var p = part.Trim();
            if (p.Length >= 2)
            {
                set.Add(p);
            }
        }

        return set;
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? string.Empty : s.Length <= max ? s : s[..max] + "...";
}

public static class KnowledgeHitFormatter
{
    public static string FormatForPrompt(IReadOnlyList<KnowledgeHit> hits)
    {
        if (hits.Count == 0)
        {
            return "(无命中知识)";
        }

        return string.Join("\n---\n", hits.Select((h, i) =>
            $"#{i + 1} [{h.Domain}] {h.Title} (score={h.Score:0.##}, source={h.Source})\n{h.Snippet}"));
    }
}

/// <summary>无种子文件时的内置语料，保证开箱可跑。</summary>
internal static class EmbeddedSeedCorpus
{
    public static IReadOnlyList<KnowledgeDocument> Documents { get; } =
    [
        new()
        {
            Id = "aftersales-return",
            Title = "售后退换货政策",
            Domain = "aftersales",
            Source = "embedded:aftersales-return.md",
            Content = """
domain: aftersales

## 退货时限
自签收之日起 7 日内，商品未使用、包装完好、配件齐全，可申请无理由退货。
食品、定制类、已激活设备不支持无理由退货。

## 换货
质量问题 15 日内可换同型号；需提供订单号与问题照片。

## 运费
无理由退货运费由买家承担；质量问题退换由平台承担。

## 流程
用户提交售后单 → 审核（1 个工作日）→ 寄回 → 质检 → 退款/换货。
"""
        },
        new()
        {
            Id = "marketing-coupon",
            Title = "营销优惠券规则",
            Domain = "marketing",
            Source = "embedded:marketing-coupon.md",
            Content = """
domain: marketing

## 新客券
新注册用户可领 20 元满 100 减券，有效期 7 天，不可与其它满减叠加。

## 会员日
每月 18 日会员专享 9 折，仅限自营标品，不包含医疗器械特价区。

## 发券限制
同一用户同一活动券最多领取 1 张；风控标记账号不可领。
发送优惠券属于写操作，需要业务确认。
"""
        },
        new()
        {
            Id = "inventory-faq",
            Title = "库存常见问题",
            Domain = "inventory",
            Source = "embedded:inventory-faq.md",
            Content = """
domain: inventory

## 查库存怎么说
用户只需提供 SKU、品名或编码；Agent 先检索产品/物料，再查库存账，不要要求数据库 Id。

## 现存与预留
现存(qtyOnHand) 是物理库存；预留(qtyReserved) 是已占用；可售 ≈ 现存 - 预留。

## 多仓
未指定仓库时返回各仓汇总；用户说「华东仓」时再按仓库过滤。
"""
        },
        new()
        {
            Id = "compete-notes",
            Title = "竞品观察要点（内部）",
            Domain = "compete",
            Source = "embedded:compete-notes.md",
            Content = """
domain: compete

## 原则
竞品结论必须标注来源；禁止编造对方销量或未公开价格。

## 对比维度
价格带、交付周期、售后政策、设备联网能力、渠道覆盖。

## 话术
对外表述用「公开资料显示」「客户反馈」，避免绝对化攻击性用语。
"""
        }
    ];
}
