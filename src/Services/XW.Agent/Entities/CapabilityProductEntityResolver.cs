using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using XW.Agent.Application.Abstractions;
using XW.Agent.Application.Models;
using XW.Agent.Application.Planning;

namespace XW.Agent.Application.Entities;

/// <summary>自然语言商品 → 主档实体（产品/物料）。</summary>
public interface IProductEntityResolver
{
    Task<ProductResolveResult> ResolveAsync(
        AgentSession session,
        string productName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 经现有 products.search / materials.search Capability 解析；低置信返回选项供确认。
/// </summary>
public sealed class CapabilityProductEntityResolver(
    IToolRegistry tools,
    ILogger<CapabilityProductEntityResolver> logger) : IProductEntityResolver
{
    private const double AutoAcceptScore = 0.85;
    private const double CandidateMinScore = 0.35;

    /// <inheritdoc />
    public async Task<ProductResolveResult> ResolveAsync(
        AgentSession session,
        string productName,
        CancellationToken cancellationToken = default)
    {
        var name = ProductNameNormalizer.Clean(productName);
        if (name.Length == 0)
        {
            name = ProductNameNormalizer.Extract(productName) ?? string.Empty;
        }

        if (name.Length == 0)
        {
            return new ProductResolveResult
            {
                Success = false,
                NeedConfirm = true,
                Question = "请提供要查询/采购的商品名称或 SKU。",
                Summary = "product_name_empty"
            };
        }

        var intent = new IntentResult
        {
            Intent = "entity.resolve",
            Confidence = 1,
            Slots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["keywords"] = name
            }
        };
        session.Slots["keywords"] = name;

        var productHits = await SearchAsync("products.search", session, intent, cancellationToken);
        var materialHits = await SearchAsync("materials.search", session, intent, cancellationToken);

        logger.LogInformation(
            "ProductResolver hits name={Name} products={Products} materials={Materials}",
            name, productHits.Count, materialHits.Count);

        var scored = ScoreHits(name, productHits, isProduct: true)
            .Concat(ScoreHits(name, materialHits, isProduct: false))
            .OrderByDescending(x => x.Score)
            .ToList();

        if (scored.Count == 0)
        {
            logger.LogInformation("ProductResolver no hit name={Name}", name);
            return new ProductResolveResult
            {
                Success = false,
                NeedConfirm = true,
                Question = $"没有找到与「{name}」匹配的商品，请确认型号/SKU，或换一个品名。",
                Summary = "no_match"
            };
        }

        var best = scored[0];
        var close = scored
            .Where(x => x.Score >= CandidateMinScore)
            .Take(5)
            .ToList();

        // 产品与物料同时高置信 → 强制消歧，禁止静默偏产品
        var topProduct = close.FirstOrDefault(x => x.ProductId is > 0);
        var topMaterial = close.FirstOrDefault(x => x.MaterialId is > 0);
        if (topProduct is not null
            && topMaterial is not null
            && topProduct.Score >= AutoAcceptScore - 0.05
            && topMaterial.Score >= AutoAcceptScore - 0.05)
        {
            logger.LogInformation(
                "ProductResolver needConfirm name={Name} reason=product_and_material",
                name);
            return new ProductResolveResult
            {
                Success = false,
                NeedConfirm = true,
                Confidence = Math.Max(topProduct.Score, topMaterial.Score),
                Question = $"「{name}」同时匹配到产品与物料，请选择一项：",
                Options = close.Where(x => x.ProductId is > 0 || x.MaterialId is > 0).Take(5).ToList(),
                Summary = "product_material_ambiguous"
            };
        }

        // 唯一高置信 → 自动采用
        if (best.Score >= AutoAcceptScore
            && (close.Count == 1 || close[1].Score < best.Score - 0.12))
        {
            logger.LogInformation(
                "ProductResolver auto name={Name} -> id={Id} sku={Sku} score={Score:0.##}",
                name, best.ProductId ?? best.MaterialId, best.Sku, best.Score);
            return new ProductResolveResult
            {
                Success = true,
                NeedConfirm = false,
                ProductId = best.ProductId,
                MaterialId = best.MaterialId,
                BaseUomId = best.BaseUomId,
                Sku = best.Sku,
                Name = best.Name,
                Confidence = best.Score,
                Summary = $"resolved:{best.Name}"
            };
        }

        logger.LogInformation(
            "ProductResolver needConfirm name={Name} candidates={Count} best={Score:0.##}",
            name, close.Count, best.Score);

        return new ProductResolveResult
        {
            Success = false,
            NeedConfirm = true,
            Confidence = best.Score,
            Question = $"「{name}」匹配到多个商品，请选择一项：",
            Options = close,
            Summary = "ambiguous"
        };
    }

    private async Task<List<JsonElement>> SearchAsync(
        string toolName,
        AgentSession session,
        IntentResult intent,
        CancellationToken ct)
    {
        var tool = tools.FindByCode(toolName);
        if (tool is null)
        {
            return [];
        }

        var sw = Stopwatch.StartNew();
        var exec = await tool.ExecuteAsync(session, intent, ct);
        sw.Stop();
        if (!exec.Success)
        {
            logger.LogWarning("ProductResolver tool={Tool} failed: {Summary}", toolName, exec.Summary);
            return [];
        }

        return ParseList(exec);
    }

    private static List<JsonElement> ParseList(ToolExecutionResult exec)
    {
        try
        {
            if (exec.Data is null)
            {
                return [];
            }

            var json = JsonSerializer.Serialize(exec.Data);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string? body = null;
            if (root.TryGetProperty("body", out var bodyEl) && bodyEl.ValueKind == JsonValueKind.String)
            {
                body = bodyEl.GetString();
            }
            else if (root.TryGetProperty("preview", out var previewEl) && previewEl.ValueKind == JsonValueKind.String)
            {
                body = previewEl.GetString();
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                return ExtractItems(root);
            }

            using var inner = JsonDocument.Parse(body);
            return ExtractItems(inner.RootElement);
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// 兼容 Host 分页：PageModelDto 用 list；部分接口用 data/items；也可能包一层 data。
    /// </summary>
    private static List<JsonElement> ExtractItems(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Array)
        {
            return el.EnumerateArray().Select(x => x.Clone()).ToList();
        }

        if (el.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        foreach (var key in new[] { "list", "items", "data", "rows" })
        {
            if (!el.TryGetProperty(key, out var child))
            {
                continue;
            }

            if (child.ValueKind == JsonValueKind.Array)
            {
                return child.EnumerateArray().Select(x => x.Clone()).ToList();
            }

            // data: { list: [...] }
            if (child.ValueKind == JsonValueKind.Object)
            {
                var nested = ExtractItems(child);
                if (nested.Count > 0)
                {
                    return nested;
                }
            }
        }

        return [];
    }

    private static IEnumerable<ProductResolveOption> ScoreHits(
        string query,
        List<JsonElement> items,
        bool isProduct)
    {
        foreach (var item in items)
        {
            var id = TryGetLong(item, "id");
            var code = TryGetString(item, "code") ?? TryGetString(item, "sku");
            var name = TryGetString(item, "name") ?? string.Empty;
            var baseUomId = TryGetLong(item, "baseUomId") ?? TryGetLong(item, "BaseUomId");
            if (id is null && string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var score = ScoreName(query, name, code);
            yield return new ProductResolveOption
            {
                ProductId = isProduct ? id : null,
                MaterialId = isProduct ? null : id,
                BaseUomId = baseUomId,
                Sku = code,
                Name = string.IsNullOrWhiteSpace(name) ? (code ?? id?.ToString() ?? "?") : name,
                Score = score
            };
        }
    }

    private static double ScoreName(string query, string name, string? code)
    {
        var q = query.Trim();
        if (q.Length == 0)
        {
            return 0;
        }

        if (!string.IsNullOrWhiteSpace(code)
            && string.Equals(code, q, StringComparison.OrdinalIgnoreCase))
        {
            return 1.0;
        }

        if (string.Equals(name, q, StringComparison.OrdinalIgnoreCase))
        {
            return 0.98;
        }

        if (name.Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return 0.9;
        }

        if (q.Contains(name, StringComparison.OrdinalIgnoreCase) && name.Length >= 2)
        {
            return 0.82;
        }

        // 简单字符重叠
        var overlap = name.Where(ch => q.Contains(ch, StringComparison.OrdinalIgnoreCase)).Distinct().Count();
        var denom = Math.Max(name.Length, 1);
        return Math.Clamp(overlap / (double)denom, 0, 0.75);
    }

    private static long? TryGetLong(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p))
        {
            return null;
        }

        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var n))
        {
            return n;
        }

        if (p.ValueKind == JsonValueKind.String
            && long.TryParse(p.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s))
        {
            return s;
        }

        return null;
    }

    private static string? TryGetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;
}
