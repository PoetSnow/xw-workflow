using System.Text.RegularExpressions;

namespace XW.Agent.Application.Entities;

/// <summary>
/// 结构清洗：按句式锚点裁剪品名，不维护口语同义词堆叠表。
/// 主路径应由 LLM Entity 抽出干净 product；此处仅做兜底与校验。
/// </summary>
public static class ProductNameNormalizer
{
    private static readonly Regex TrailingStockSpeak = new(
        @"(?:还有|的)?(?:多少|数量|情况|余额)?\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>从整句按结构锚点抽出品名。</summary>
    public static string? Extract(string? message)
    {
        var text = (message ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return null;
        }

        var stockIdx = text.IndexOf("库存", StringComparison.Ordinal);
        if (stockIdx > 0)
        {
            var head = Clean(text[..stockIdx]);
            if (IsPlausibleName(head))
            {
                return head;
            }
        }

        foreach (var marker in new[] { "少于", "低于", "不足", "小于", "如果" })
        {
            var idx = text.IndexOf(marker, StringComparison.Ordinal);
            if (idx > 0)
            {
                var cut = Clean(text[..idx]);
                if (IsPlausibleName(cut))
                {
                    return cut;
                }
            }
        }

        var fallback = Clean(text);
        return IsPlausibleName(fallback) ? fallback : null;
    }

    /// <summary>清洗已抽出片段：按锚点截断 + 去掉库存口语尾巴。</summary>
    public static string Clean(string? raw)
    {
        var s = (raw ?? string.Empty).Trim();
        if (s.Length == 0)
        {
            return string.Empty;
        }

        var stockIdx = s.IndexOf("库存", StringComparison.Ordinal);
        if (stockIdx >= 0)
        {
            s = s[..stockIdx].Trim();
        }

        foreach (var marker in new[] { "少于", "低于", "不足", "小于", "如果", "就帮我采购", "就采购", "帮我采购" })
        {
            var idx = s.IndexOf(marker, StringComparison.Ordinal);
            if (idx > 0)
            {
                s = s[..idx].Trim();
                break;
            }
        }

        // 结构祈使壳：仅匹配句首「(请|帮我|麻烦)?(看|查|查询)?(一下|下)?」一次，不是同义词堆叠
        s = Regex.Replace(
            s,
            @"^(?:请|帮我|麻烦)?(?:看|查|查询)?(?:一下|下)?\s*",
            string.Empty,
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        s = TrailingStockSpeak.Replace(s, string.Empty);
        return s.Trim('的', ' ', '　', '，', ',', '、', '：', ':');
    }

    private static bool IsPlausibleName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var n = name.Trim();
        if (n.Length is < 1 or > 64)
        {
            return false;
        }

        if (n is "一下" or "下" or "货" or "商品" or "产品")
        {
            return false;
        }

        return !n.Contains("库存", StringComparison.Ordinal)
               && !n.Contains("少于", StringComparison.Ordinal)
               && !n.Contains("采购", StringComparison.Ordinal);
    }
}
