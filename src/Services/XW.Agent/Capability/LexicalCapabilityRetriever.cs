using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XW.Agent.Application.Options;

namespace XW.Agent.Application.Capability;

/// <summary>Capability 动态检索请求。</summary>
public sealed class CapabilityRetrievalRequest
{
    /// <summary>用户查询原文。</summary>
    public required string Query { get; init; }

    /// <summary>已过权限过滤的候选全集。</summary>
    public required IReadOnlyList<AgentCapabilityDefinition> Candidates { get; init; }

    /// <summary>多步编排中必须保留的能力（已调用过 / Related）。</summary>
    public IReadOnlyList<string> KeepCodes { get; init; } = [];

    /// <summary>返回 TopK。</summary>
    public int TopK { get; init; } = 8;

    /// <summary>是否扩展 RelatedCodes。</summary>
    public bool ExpandRelated { get; init; } = true;

    /// <summary>最低分阈值。</summary>
    public double MinScore { get; init; }
}

/// <summary>单条能力检索命中。</summary>
public sealed class CapabilityRetrievalHit
{
    public required AgentCapabilityDefinition Capability { get; init; }
    public double Score { get; init; }
    public string Reason { get; init; } = string.Empty;
}

/// <summary>能力检索结果集。</summary>
public sealed class CapabilityRetrievalResult
{
    public required IReadOnlyList<CapabilityRetrievalHit> Hits { get; init; }
    /// <summary>命中能力列表（供 Planner）。</summary>
    public IReadOnlyList<AgentCapabilityDefinition> Tools => Hits.Select(h => h.Capability).ToList();
    public int CandidateCount { get; init; }
    public string Strategy { get; init; } = "lexical";
}

/// <summary>动态 Tool 检索：从注册中心挑 TopK，避免 Planner tools_json 爆炸。</summary>
public interface ICapabilityRetriever
{
    Task<CapabilityRetrievalResult> RetrieveAsync(
        CapabilityRetrievalRequest request,
        CancellationToken ct = default);
}

/// <summary>
/// 词法/标签检索（可替换为向量）。接口规模上来后这是 Planner 的前置闸门。
/// </summary>
public sealed class LexicalCapabilityRetriever(
    IOptionsMonitor<AgentOptions> options,
    ILogger<LexicalCapabilityRetriever> logger) : ICapabilityRetriever
{
    private static readonly Regex TokenSplit = new(@"[\s,，。；;、/\-_|]+", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> DomainHints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["库存"] = "inventory",
        ["现存"] = "inventory",
        ["可售"] = "inventory",
        ["仓库"] = "inventory",
        ["sku"] = "inventory",
        ["产品"] = "inventory",
        ["物料"] = "inventory",
        ["订单"] = "order",
        ["履约"] = "order",
        ["物流"] = "order",
        ["发货"] = "order",
        ["预警"] = "alert",
        ["告警"] = "alert",
        ["缺货"] = "alert",
        ["断货"] = "alert",
        // 采购域：一词映射 Domain，供车道/Planner 检索共用，勿在 LaneRouter 叠词
        ["采购"] = "purchase",
        ["供应商"] = "purchase",
        ["比价"] = "purchase",
        ["进货"] = "purchase"
    };

    public Task<CapabilityRetrievalResult> RetrieveAsync(
        CapabilityRetrievalRequest request,
        CancellationToken ct = default)
    {
        var cfg = options.CurrentValue.Planner.Retrieval;
        var topK = request.TopK > 0 ? request.TopK : Math.Max(1, cfg.TopK);
        var minScore = request.MinScore > 0 ? request.MinScore : cfg.MinScore;
        var expand = request.ExpandRelated || cfg.ExpandRelated;

        var query = request.Query?.Trim() ?? string.Empty;
        var tokens = Tokenize(query);
        var hintedDomains = DomainHints
            .Where(kv => query.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var byCode = request.Candidates
            .GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var scored = new Dictionary<string, CapabilityRetrievalHit>(StringComparer.OrdinalIgnoreCase);

        foreach (var cap in request.Candidates)
        {
            var (score, reason) = Score(cap, query, tokens, hintedDomains);
            if (score < minScore && !IsKeep(cap.Code, request.KeepCodes))
            {
                continue;
            }

            scored[cap.Code] = new CapabilityRetrievalHit
            {
                Capability = cap,
                Score = score,
                Reason = reason
            };
        }

        // KeepCodes 强制入选（多步编排不断链）
        foreach (var code in request.KeepCodes)
        {
            if (byCode.TryGetValue(code, out var cap) && !scored.ContainsKey(cap.Code))
            {
                scored[cap.Code] = new CapabilityRetrievalHit
                {
                    Capability = cap,
                    Score = Math.Max(minScore, 0.01),
                    Reason = "keep"
                };
            }
        }

        // Related 扩展：命中能力带出编排链上的邻居
        if (expand)
        {
            var seeds = scored.Values
                .OrderByDescending(h => h.Score)
                .Take(Math.Max(3, topK / 2))
                .Select(h => h.Capability)
                .ToList();

            foreach (var seed in seeds)
            {
                foreach (var related in seed.RelatedCodes)
                {
                    if (!byCode.TryGetValue(related, out var cap) || scored.ContainsKey(cap.Code))
                    {
                        continue;
                    }

                    scored[cap.Code] = new CapabilityRetrievalHit
                    {
                        Capability = cap,
                        Score = Math.Max(minScore, scored.GetValueOrDefault(seed.Code)?.Score * 0.7 ?? 0.2),
                        Reason = $"related:{seed.Code}"
                    };
                }
            }
        }

        // 无命中时兜底：按域 hint 或整体 TopK（避免空工具）
        if (scored.Count == 0 && request.Candidates.Count > 0)
        {
            var fallback = request.Candidates
                .Where(c => hintedDomains.Count == 0
                            || hintedDomains.Contains(c.Domain)
                            || string.IsNullOrWhiteSpace(c.Domain))
                .Take(topK)
                .ToList();

            if (fallback.Count == 0)
            {
                fallback = request.Candidates.Take(topK).ToList();
            }

            foreach (var cap in fallback)
            {
                scored[cap.Code] = new CapabilityRetrievalHit
                {
                    Capability = cap,
                    Score = 0.01,
                    Reason = "fallback"
                };
            }
        }

        var hits = scored.Values
            .OrderByDescending(h => h.Score)
            .ThenBy(h => h.Capability.Code, StringComparer.OrdinalIgnoreCase)
            .Take(topK)
            .ToList();

        logger.LogInformation(
            "Capability retrieve queryLen={Len} candidates={Cand} hits={Hits} top=[{Top}]",
            query.Length,
            request.Candidates.Count,
            hits.Count,
            string.Join(",", hits.Select(h => $"{h.Capability.Code}:{h.Score:0.##}")));

        return Task.FromResult(new CapabilityRetrievalResult
        {
            Hits = hits,
            CandidateCount = request.Candidates.Count,
            Strategy = "lexical+related"
        });
    }

    private static bool IsKeep(string code, IReadOnlyList<string> keep) =>
        keep.Any(k => string.Equals(k, code, StringComparison.OrdinalIgnoreCase));

    private static (double Score, string Reason) Score(
        AgentCapabilityDefinition cap,
        string query,
        HashSet<string> tokens,
        HashSet<string> hintedDomains)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return (0, "empty_query");
        }

        double score = 0;
        var reasons = new List<string>();

        if (!string.IsNullOrWhiteSpace(cap.Domain) && hintedDomains.Contains(cap.Domain))
        {
            score += 2.5;
            reasons.Add("domain");
        }

        if (ContainsIgnoreCase(cap.Code, query) || tokens.Any(t => ContainsIgnoreCase(cap.Code, t)))
        {
            score += 3;
            reasons.Add("code");
        }

        if (ContainsIgnoreCase(cap.Name, query) || tokens.Any(t => ContainsIgnoreCase(cap.Name, t)))
        {
            score += 2.5;
            reasons.Add("name");
        }

        foreach (var tag in cap.Tags)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                continue;
            }

            // 完整包含，或 token 与 tag 互相包含（「采购」命中标签「采购单」）
            if (ContainsIgnoreCase(query, tag)
                || tokens.Any(t =>
                    t.Length >= 2
                    && (string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)
                        || ContainsIgnoreCase(tag, t)
                        || ContainsIgnoreCase(t, tag))))
            {
                score += 2;
                reasons.Add("tag");
                break;
            }
        }

        foreach (var alias in cap.Aliases)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                continue;
            }

            if (ContainsIgnoreCase(query, alias)
                || ContainsIgnoreCase(alias, query)
                || tokens.Any(t =>
                    t.Length >= 2
                    && (ContainsIgnoreCase(alias, t) || ContainsIgnoreCase(t, alias))))
            {
                score += 3;
                reasons.Add("alias");
                break;
            }
        }

        if (!string.IsNullOrWhiteSpace(cap.WhenToUse))
        {
            var hit = tokens.Count(t => t.Length >= 2 && ContainsIgnoreCase(cap.WhenToUse, t));
            if (hit > 0)
            {
                score += Math.Min(2.5, hit * 0.8);
                reasons.Add("when");
            }
        }

        if (!string.IsNullOrWhiteSpace(cap.Description))
        {
            var hit = tokens.Count(t => t.Length >= 2 && ContainsIgnoreCase(cap.Description, t));
            if (hit > 0)
            {
                score += Math.Min(2, hit * 0.5);
                reasons.Add("desc");
            }
        }

        // 短中文整句包含
        if (query.Length >= 2 && (
                ContainsIgnoreCase(cap.Description, query)
                || ContainsIgnoreCase(cap.WhenToUse, query)
                || ContainsIgnoreCase(cap.Name, query)))
        {
            score += 1.5;
            reasons.Add("phrase");
        }

        return (score, reasons.Count == 0 ? "none" : string.Join("+", reasons.Distinct()));
    }

    private static HashSet<string> Tokenize(string query)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query))
        {
            return set;
        }

        foreach (var part in TokenSplit.Split(query))
        {
            var p = part.Trim();
            if (p.Length >= 2)
            {
                set.Add(p);
            }
        }

        // 中文 2-gram，提升「查库存」类短句召回
        var sb = new StringBuilder();
        foreach (var ch in query)
        {
            if (char.IsLetterOrDigit(ch) || ch is >= '\u4e00' and <= '\u9fff')
            {
                sb.Append(ch);
            }
            else if (sb.Length > 0)
            {
                AddGrams(set, sb.ToString());
                sb.Clear();
            }
        }

        if (sb.Length > 0)
        {
            AddGrams(set, sb.ToString());
        }

        return set;
    }

    private static void AddGrams(HashSet<string> set, string s)
    {
        if (s.Length >= 2)
        {
            set.Add(s);
        }

        for (var i = 0; i + 1 < s.Length; i++)
        {
            set.Add(s.Substring(i, 2));
        }
    }

    private static bool ContainsIgnoreCase(string? hay, string needle) =>
        !string.IsNullOrEmpty(hay)
        && !string.IsNullOrEmpty(needle)
        && hay.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
