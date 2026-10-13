using System.Globalization;
using System.Text.Json;
using XW.Agent.Application.Models;

namespace XW.Agent.Application.Runtime.Pending;

/// <summary>
/// 待办快照：在 session.Slots 上读写结构化状态，避免散落魔法字符串。
/// </summary>
public static class PendingTaskSnapshot
{
    private static readonly string[] PurchaseBusinessSlots =
    [
        "supplierId",
        "unitPrice",
        "qty",
        "purchaseQuantity",
        "uomId",
        "_confirmed",
        "_resolved_supplier",
        "_price_compare_supplier_count",
        PendingTaskKinds.SlotSupplierCandidates,
        PendingTaskKinds.SlotIdempotencyKey,
        PendingTaskKinds.SlotLineIdempotencyKey
    ];

    public static bool HasPending(AgentSession session) =>
        session.Slots.TryGetValue(PendingTaskKinds.SlotKind, out var kind) && !string.IsNullOrWhiteSpace(kind);

    public static string? GetKind(AgentSession session) =>
        session.Slots.TryGetValue(PendingTaskKinds.SlotKind, out var kind) ? kind : null;

    public static string? GetStep(AgentSession session) =>
        session.Slots.TryGetValue(PendingTaskKinds.SlotStep, out var step) ? step : null;

    public static void Set(AgentSession session, string kind, string step)
    {
        session.Slots[PendingTaskKinds.SlotKind] = kind;
        session.Slots[PendingTaskKinds.SlotStep] = step;
    }

    public static void ClearMeta(AgentSession session)
    {
        session.Slots.Remove(PendingTaskKinds.SlotKind);
        session.Slots.Remove(PendingTaskKinds.SlotStep);
    }

    /// <summary>取消采购待办：清 meta + 业务槽，保留 productId/keywords 便于再查。</summary>
    public static void ClearPurchase(AgentSession session)
    {
        ClearMeta(session);
        foreach (var key in PurchaseBusinessSlots)
        {
            session.Slots.Remove(key);
        }
    }

    /// <summary>挂起时生成并复用幂等键，直到 Clear。</summary>
    public static void EnsureIdempotencyKeys(AgentSession session)
    {
        if (!session.Slots.TryGetValue(PendingTaskKinds.SlotIdempotencyKey, out var key)
            || string.IsNullOrWhiteSpace(key))
        {
            key = Guid.NewGuid().ToString("N");
            session.Slots[PendingTaskKinds.SlotIdempotencyKey] = key;
        }

        if (!session.Slots.TryGetValue(PendingTaskKinds.SlotLineIdempotencyKey, out var lineKey)
            || string.IsNullOrWhiteSpace(lineKey))
        {
            lineKey = Guid.NewGuid().ToString("N");
            session.Slots[PendingTaskKinds.SlotLineIdempotencyKey] = lineKey;
        }

        session.Slots["idempotencyKey"] = key!;
        session.Slots["lineIdempotencyKey"] = lineKey!;
    }

    public static void ArmPurchaseAfterLowStock(AgentSession session)
    {
        EnsureIdempotencyKeys(session);
        Set(session, PendingTaskKinds.KindPurchaseCreate, PendingTaskKinds.StepAwaitConfirm);
    }

    public static void ArmAwaitSupplierPrice(AgentSession session)
    {
        EnsureIdempotencyKeys(session);
        Set(session, PendingTaskKinds.KindPurchaseCreate, PendingTaskKinds.StepAwaitSupplierPrice);
    }

    public static void ArmAwaitFinalConfirm(AgentSession session)
    {
        EnsureIdempotencyKeys(session);
        Set(session, PendingTaskKinds.KindPurchaseCreate, PendingTaskKinds.StepAwaitFinalConfirm);
        session.Slots["_confirmed"] = "false";
    }

    public static void MarkWriteConfirmed(AgentSession session)
    {
        session.Slots["_confirmed"] = "true";
    }

    /// <summary>
    /// 是否允许把本轮抬成写确认。只有待办已停在最终确认步、且用户这次是肯定答复时才返回 true。
    /// 补槽、选供应商、首轮「同意进入采购」都不抬确认，避免未看清单就 POST。
    /// </summary>
    public static bool ShouldElevateWriteConfirmation(AgentSession session, PendingTaskReplyKind reply) =>
        reply == PendingTaskReplyKind.Affirm
        && string.Equals(GetStep(session), PendingTaskKinds.StepAwaitFinalConfirm, StringComparison.OrdinalIgnoreCase);

    public static bool IsWriteConfirmed(AgentSession session) =>
        session.Slots.TryGetValue("_confirmed", out var v)
        && string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);

    public static void SaveSupplierCandidates(AgentSession session, IReadOnlyList<SupplierCandidate> candidates)
    {
        session.Slots[PendingTaskKinds.SlotSupplierCandidates] =
            JsonSerializer.Serialize(candidates);
        session.Slots["_price_compare_supplier_count"] =
            candidates.Count.ToString(CultureInfo.InvariantCulture);
    }

    public static IReadOnlyList<SupplierCandidate> LoadSupplierCandidates(AgentSession session)
    {
        if (!session.Slots.TryGetValue(PendingTaskKinds.SlotSupplierCandidates, out var raw)
            || string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<SupplierCandidate>>(raw) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public sealed class SupplierCandidate
    {
        public string SupplierId { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string? UnitPrice { get; set; }
    }
}
