using System.Text.RegularExpressions;
using XW.Agent.Application.Abstractions;
using XW.Agent.Application.Models;

namespace XW.Agent.Application.Intents;

/// <summary>
/// 关键词/正则兜底。主路径是 LLM；仅 Fallback 时使用。
/// </summary>
public sealed partial class RuleBasedIntentRouter : IIntentRouter
{
    public Task<IntentResult> RouteAsync(AgentSession session, string message, CancellationToken ct = default)
    {
        var text = message.Trim();
        var slots = new Dictionary<string, string>(session.Slots, StringComparer.OrdinalIgnoreCase);

        ExtractKeyword(text, slots);
        ExtractWarehouse(text, slots);
        ExtractOrderNo(text, slots);

        string intent;
        double confidence;
        List<string> required;

        if (ContainsAny(text, "库存", "有货", "还剩", "可售", "warehouse", "sku", "物料"))
        {
            intent = AgentIntents.QueryInventory;
            confidence = 0.85;
            required = ["keyword"];
        }
        else if (ContainsAny(text, "订单", "发货", "履约", "物流", "到哪了", "order"))
        {
            intent = AgentIntents.QueryOrderFulfillment;
            confidence = 0.85;
            required = ["orderNo"];
        }
        else if (ContainsAny(text, "预警", "告警", "低库存", "缺货风险"))
        {
            intent = AgentIntents.ListLowStockAlerts;
            confidence = 0.8;
            required = [];
        }
        else
        {
            intent = AgentIntents.Unknown;
            confidence = 0.2;
            required = [];
        }

        foreach (var (k, v) in slots)
        {
            session.Slots[k] = v;
        }

        var missing = required
            .Where(r => !session.Slots.ContainsKey(r) || string.IsNullOrWhiteSpace(session.Slots[r]))
            .ToList();

        return Task.FromResult(new IntentResult
        {
            Intent = intent,
            Confidence = confidence,
            Slots = new Dictionary<string, string>(session.Slots, StringComparer.OrdinalIgnoreCase),
            MissingSlots = missing
        });
    }

    private static bool ContainsAny(string text, params string[] keys) =>
        keys.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

    private static void ExtractKeyword(string text, Dictionary<string, string> slots)
    {
        var sku = SkuRegex().Match(text);
        if (sku.Success)
        {
            slots["keyword"] = sku.Groups[1].Value.ToUpperInvariant();
            return;
        }

        var labeled = KeywordLabeledRegex().Match(text);
        if (labeled.Success)
        {
            slots["keyword"] = labeled.Groups[1].Value.Trim();
            return;
        }

        // 「查一下阿莫西林库存」：去掉套话后剩余短词作 keyword
        var cleaned = NoiseRegex().Replace(text, " ").Trim();
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        if (cleaned.Length is >= 2 and <= 32
            && !cleaned.Contains('？')
            && !string.Equals(cleaned, "库存", StringComparison.Ordinal))
        {
            slots["keyword"] = cleaned;
        }
    }

    private static void ExtractWarehouse(string text, Dictionary<string, string> slots)
    {
        var m = WarehouseRegex().Match(text);
        if (m.Success)
        {
            slots["warehouse"] = m.Groups[0].Value;
        }
    }

    private static void ExtractOrderNo(string text, Dictionary<string, string> slots)
    {
        var m = OrderRegex().Match(text);
        if (m.Success)
        {
            slots["orderNo"] = m.Groups[1].Value.ToUpperInvariant();
        }
    }

    [GeneratedRegex(@"\b([A-Za-z]{1,8}\d{2,}[A-Za-z0-9\-]*)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SkuRegex();

    [GeneratedRegex(@"(?:物料|商品|产品|药品|SKU|sku)\s*[=:：]?\s*([^\s,，。；;]{1,32})", RegexOptions.IgnoreCase)]
    private static partial Regex KeywordLabeledRegex();

    [GeneratedRegex(@"帮我|查一下|查询|看看|库存|有没有|还剩多少|可售", RegexOptions.IgnoreCase)]
    private static partial Regex NoiseRegex();

    [GeneratedRegex(@"([华北华东华南华中西南西北东北]|[一二三四五六七八九])?仓", RegexOptions.IgnoreCase)]
    private static partial Regex WarehouseRegex();

    [GeneratedRegex(@"\b((?:SO|DO|ORD)[-_]?\d{6,})\b", RegexOptions.IgnoreCase)]
    private static partial Regex OrderRegex();
}
