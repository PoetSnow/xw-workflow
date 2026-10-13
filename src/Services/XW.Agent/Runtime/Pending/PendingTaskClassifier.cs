using System.Globalization;
using System.Text.RegularExpressions;
using XW.Agent.Application.Models;

namespace XW.Agent.Application.Runtime.Pending;

/// <summary>
/// 待办语境下分类用户回复：状态条件，不是全量意图词表。
/// </summary>
public static class PendingTaskClassifier
{
    private static readonly Regex SupplierIdRx = new(
        @"(?:供应商Id|supplierId|供应商)\s*[=:：]?\s*(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex UnitPriceRx = new(
        @"(?:单价|unitPrice|价格)\s*[=:：]?\s*(\d+(?:\.\d+)?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex OptionIndexRx = new(
        @"^(?:选(?:择)?|用|第)?\s*(\d{1,2})\s*(?:号|家)?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static PendingTaskReplyKind Classify(AgentSession session, string message)
    {
        if (!PendingTaskSnapshot.HasPending(session))
        {
            return PendingTaskReplyKind.Unrelated;
        }

        var text = (message ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return PendingTaskReplyKind.Unrelated;
        }

        if (IsCancel(text))
        {
            return PendingTaskReplyKind.Cancel;
        }

        if (TryParseSupplierPrice(text, out var supplierId, out var unitPrice))
        {
            if (!string.IsNullOrWhiteSpace(supplierId))
            {
                session.Slots["supplierId"] = supplierId!;
            }

            if (!string.IsNullOrWhiteSpace(unitPrice))
            {
                session.Slots["unitPrice"] = unitPrice!;
            }

            return PendingTaskReplyKind.ProvideSlots;
        }

        var step = PendingTaskSnapshot.GetStep(session);
        if (string.Equals(step, PendingTaskKinds.StepAwaitSupplierPrice, StringComparison.OrdinalIgnoreCase)
            && TrySelectSupplierOption(session, text))
        {
            return PendingTaskReplyKind.SelectOption;
        }

        if (IsAffirm(text))
        {
            return PendingTaskReplyKind.Affirm;
        }

        if (text.Contains("库存", StringComparison.Ordinal)
            || text.Contains("还有多少", StringComparison.Ordinal))
        {
            return PendingTaskReplyKind.Unrelated;
        }

        // 仅在「等待确认」类步骤允许极短肯定词；不再把任意 ≤16 字短句当 Affirm
        if ((string.Equals(step, PendingTaskKinds.StepAwaitConfirm, StringComparison.OrdinalIgnoreCase)
             || string.Equals(step, PendingTaskKinds.StepAwaitFinalConfirm, StringComparison.OrdinalIgnoreCase))
            && text.Length <= 4
            && !LooksLikeNewTopic(text)
            && IsSoftAffirm(text))
        {
            return PendingTaskReplyKind.Affirm;
        }

        return PendingTaskReplyKind.Unrelated;
    }

    public static bool TryParseSupplierPrice(string text, out string? supplierId, out string? unitPrice)
    {
        supplierId = null;
        unitPrice = null;
        var sm = SupplierIdRx.Match(text);
        if (sm.Success)
        {
            supplierId = sm.Groups[1].Value;
        }

        var pm = UnitPriceRx.Match(text);
        if (pm.Success
            && decimal.TryParse(pm.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
        {
            unitPrice = pm.Groups[1].Value;
        }

        return supplierId is not null || unitPrice is not null;
    }

    private static bool TrySelectSupplierOption(AgentSession session, string text)
    {
        var m = OptionIndexRx.Match(text.Trim());
        if (!m.Success)
        {
            return false;
        }

        if (!int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
            || index < 1)
        {
            return false;
        }

        var candidates = PendingTaskSnapshot.LoadSupplierCandidates(session);
        if (candidates.Count == 0 || index > candidates.Count)
        {
            return false;
        }

        var pick = candidates[index - 1];
        if (!string.IsNullOrWhiteSpace(pick.SupplierId))
        {
            session.Slots["supplierId"] = pick.SupplierId;
        }

        if (!string.IsNullOrWhiteSpace(pick.UnitPrice))
        {
            session.Slots["unitPrice"] = pick.UnitPrice!;
        }

        if (!string.IsNullOrWhiteSpace(pick.SupplierName))
        {
            session.Slots["_resolved_supplier"] = pick.SupplierName;
        }

        return true;
    }

    private static bool IsCancel(string text) =>
        text is "取消" or "算了" or "不要" or "停"
        || text.Contains("取消采购", StringComparison.Ordinal)
        || text.Contains("不用了", StringComparison.Ordinal);

    private static bool IsAffirm(string text) =>
        text is "是" or "好" or "好的" or "行" or "可以" or "确认" or "继续" or "下单" or "草稿"
        || text.Contains("创建草稿", StringComparison.Ordinal)
        || text.Contains("生成草稿", StringComparison.Ordinal)
        || text.Contains("采购草稿", StringComparison.Ordinal)
        || text.Contains("确认创建", StringComparison.Ordinal)
        || text.Contains("确认下单", StringComparison.Ordinal)
        || text.Equals("创建采购草稿", StringComparison.Ordinal);

    private static bool IsSoftAffirm(string text) =>
        text is "嗯" or "哦" or "OK" or "ok" or "Ok" or "yes" or "Yes";

    private static bool LooksLikeNewTopic(string text) =>
        text.Contains("订单", StringComparison.Ordinal)
        || text.Contains("物流", StringComparison.Ordinal)
        || text.Contains("知识", StringComparison.Ordinal)
        || text.Contains("怎么", StringComparison.Ordinal);
}
