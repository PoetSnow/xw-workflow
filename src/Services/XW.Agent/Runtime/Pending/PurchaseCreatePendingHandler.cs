using System.Globalization;
using System.Text;
using System.Text.Json;
using XW.Agent.Application.Models;
using XW.Agent.Application.Options;
using XW.Agent.Application.Planning;

namespace XW.Agent.Application.Runtime.Pending;

/// <summary>
/// 采购草稿待办：库存不足 → 确认进入 → 比价/选商 → 最终确认 → create。
/// </summary>
public sealed class PurchaseCreatePendingHandler : IPendingTaskHandler
{
    public string Kind => PendingTaskKinds.KindPurchaseCreate;

    public void OnCancel(AgentSession session) => PendingTaskSnapshot.ClearPurchase(session);

    public void OnToolSuccess(AgentSession session, string toolCode, object? data)
    {
        if (string.Equals(toolCode, "purchase.price.compare", StringComparison.OrdinalIgnoreCase))
        {
            BindPriceCompareData(session, data);
            return;
        }

        if (toolCode.StartsWith("purchase.order.create", StringComparison.OrdinalIgnoreCase))
        {
            PendingTaskSnapshot.ClearPurchase(session);
        }
    }

    public bool TryPlan(
        AgentSession session,
        PendingTaskReplyKind reply,
        IReadOnlyList<AgentCapabilityDefinition> allowedCaps,
        out AgentPlanDecision decision)
    {
        decision = null!;
        var step = PendingTaskSnapshot.GetStep(session) ?? PendingTaskKinds.StepAwaitConfirm;

        // 补槽 / 选供应商：只写槽，不放行 create
        if (reply is PendingTaskReplyKind.ProvideSlots or PendingTaskReplyKind.SelectOption)
        {
            if (HasCreateReadySlots(session, out _))
            {
                PendingTaskSnapshot.ArmAwaitFinalConfirm(session);
                decision = BuildFinalConfirmClarify(session, "已补全供应商与单价");
                return true;
            }

            decision = BuildMissingSlotsClarify(session);
            return true;
        }

        if (reply != PendingTaskReplyKind.Affirm)
        {
            return false;
        }

        // 最终确认 → 放行写入
        if (string.Equals(step, PendingTaskKinds.StepAwaitFinalConfirm, StringComparison.OrdinalIgnoreCase))
        {
            PendingTaskSnapshot.MarkWriteConfirmed(session);
            PendingTaskSnapshot.EnsureIdempotencyKeys(session);
            return TryBuildCreateOrMissing(session, allowedCaps, out decision);
        }

        // 首轮确认进入采购 → 比价（尚未最终授权 create）
        if (string.Equals(step, PendingTaskKinds.StepAwaitConfirm, StringComparison.OrdinalIgnoreCase)
            || string.Equals(step, PendingTaskKinds.StepAwaitSupplierPrice, StringComparison.OrdinalIgnoreCase))
        {
            // 若已齐槽且用户再次确认 → 进入最终确认或直接 create
            if (HasCreateReadySlots(session, out _))
            {
                if (string.Equals(step, PendingTaskKinds.StepAwaitSupplierPrice, StringComparison.OrdinalIgnoreCase))
                {
                    PendingTaskSnapshot.ArmAwaitFinalConfirm(session);
                    decision = BuildFinalConfirmClarify(session, "供应商与单价已就绪");
                    return true;
                }
            }

            return TryBuildPriceCompare(session, allowedCaps, out decision);
        }

        return false;
    }

    public bool TryAfterTool(
        AgentSession session,
        IReadOnlyList<AgentCapabilityDefinition> allowedCaps,
        IReadOnlyList<AgentPlanObservation> observations,
        IReadOnlyList<string> summaries,
        out AgentPlanDecision decision)
    {
        decision = null!;
        var price = observations.LastOrDefault(o =>
            o.Success
            && string.Equals(o.Tool, "purchase.price.compare", StringComparison.OrdinalIgnoreCase));
        if (price is null)
        {
            return false;
        }

        BindPriceCompareFromLastObservation(session, observations);

        var candidates = PendingTaskSnapshot.LoadSupplierCandidates(session);
        var fact = summaries.Count == 0 ? price.Summary : string.Join(" | ", summaries);
        session.Slots.TryGetValue("_resolved_product", out var name);
        session.Slots.TryGetValue("purchaseQuantity", out var qty);
        var subject = string.IsNullOrWhiteSpace(name) ? "该商品" : name!.Trim();
        var qtyHint = string.IsNullOrWhiteSpace(qty) ? "" : $"，数量 {qty}";

        if (candidates.Count == 0)
        {
            PendingTaskSnapshot.ArmAwaitSupplierPrice(session);
            decision = new AgentPlanDecision
            {
                Action = "clarify",
                Question = $"{fact}\n暂无历史采购价（{subject}{qtyHint}）。请回复 `供应商Id=… 单价=…`，或「取消」。",
                Reason = "purchase_no_price_history"
            };
            return true;
        }

        if (candidates.Count == 1)
        {
            var one = candidates[0];
            if (!string.IsNullOrWhiteSpace(one.SupplierId))
            {
                session.Slots["supplierId"] = one.SupplierId;
            }

            if (!string.IsNullOrWhiteSpace(one.UnitPrice))
            {
                session.Slots["unitPrice"] = one.UnitPrice!;
            }

            if (!string.IsNullOrWhiteSpace(one.SupplierName))
            {
                session.Slots["_resolved_supplier"] = one.SupplierName;
            }

            PendingTaskSnapshot.ArmAwaitFinalConfirm(session);
            decision = BuildFinalConfirmClarify(
                session,
                $"{fact}\n已自动锁定唯一供应商「{one.SupplierName}」单价 {one.UnitPrice}");
            return true;
        }

        PendingTaskSnapshot.ArmAwaitSupplierPrice(session);
        var sb = new StringBuilder();
        sb.Append(fact).Append('\n');
        sb.Append($"采购待办（{subject}{qtyHint}）：共 {candidates.Count} 家供应商，请回复序号（如 `1`）或 `供应商Id=… 单价=…`：\n");
        for (var i = 0; i < candidates.Count && i < 8; i++)
        {
            var c = candidates[i];
            sb.Append(i + 1)
                .Append(". Id=")
                .Append(c.SupplierId)
                .Append(' ')
                .Append(c.SupplierName)
                .Append(" 单价=")
                .Append(c.UnitPrice ?? "?")
                .Append('\n');
        }

        decision = new AgentPlanDecision
        {
            Action = "clarify",
            Question = sb.ToString().TrimEnd(),
            Reason = "purchase_await_supplier_pick"
        };
        return true;
    }

    /// <summary>比价 HTTP 成功后由 Orchestrator 调用，写入候选列表。</summary>
    public static void BindPriceCompareData(AgentSession session, object? data)
    {
        if (data is null)
        {
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(data);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!TryReadSuppliers(root, out var list))
            {
                foreach (var prop in new[] { "body", "preview" })
                {
                    if (!root.TryGetProperty(prop, out var el) || el.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var raw = el.GetString();
                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        continue;
                    }

                    using var inner = JsonDocument.Parse(raw);
                    if (TryReadSuppliers(inner.RootElement, out list))
                    {
                        break;
                    }
                }
            }

            if (list.Count > 0)
            {
                PendingTaskSnapshot.SaveSupplierCandidates(session, list);
            }
        }
        catch
        {
            // ignore
        }
    }

    private static void BindPriceCompareFromLastObservation(
        AgentSession session,
        IReadOnlyList<AgentPlanObservation> observations)
    {
        // 候选已在 Execute 时由 Orchestrator 调用 BindPriceCompareData 写入；此处兜底读 preview
        if (PendingTaskSnapshot.LoadSupplierCandidates(session).Count > 0)
        {
            return;
        }

        var price = observations.LastOrDefault(o =>
            string.Equals(o.Tool, "purchase.price.compare", StringComparison.OrdinalIgnoreCase));
        if (price is null || string.IsNullOrWhiteSpace(price.Preview))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(price.Preview);
            if (TryReadSuppliers(doc.RootElement, out var list) && list.Count > 0)
            {
                PendingTaskSnapshot.SaveSupplierCandidates(session, list);
            }
        }
        catch
        {
            // preview 可能不是完整 JSON
        }
    }

    private static bool TryReadSuppliers(JsonElement root, out List<PendingTaskSnapshot.SupplierCandidate> list)
    {
        list = [];
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Object)
        {
            root = data;
        }

        if (!root.TryGetProperty("bySupplier", out var bySupplier)
            || bySupplier.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var item in bySupplier.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string? id = null;
            if (item.TryGetProperty("supplierId", out var idEl))
            {
                id = idEl.ValueKind == JsonValueKind.Number ? idEl.ToString() : idEl.GetString();
            }

            var name = item.TryGetProperty("supplierName", out var sn) ? sn.GetString() : null;
            string? price = null;
            if (item.TryGetProperty("lastUnitPrice", out var last) && last.ValueKind != JsonValueKind.Null)
            {
                price = last.ToString();
            }
            else if (item.TryGetProperty("minUnitPrice", out var min) && min.ValueKind != JsonValueKind.Null)
            {
                price = min.ToString();
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            list.Add(new PendingTaskSnapshot.SupplierCandidate
            {
                SupplierId = id!,
                SupplierName = name ?? string.Empty,
                UnitPrice = price
            });
        }

        return list.Count > 0;
    }

    private static bool TryBuildPriceCompare(
        AgentSession session,
        IReadOnlyList<AgentCapabilityDefinition> allowedCaps,
        out AgentPlanDecision decision)
    {
        decision = null!;
        var hasProduct = session.Slots.TryGetValue("productId", out var productId)
                         && !string.IsNullOrWhiteSpace(productId);
        var hasMaterial = session.Slots.TryGetValue("materialId", out var materialId)
                          && !string.IsNullOrWhiteSpace(materialId);
        if (!hasProduct && !hasMaterial)
        {
            decision = new AgentPlanDecision
            {
                Action = "clarify",
                Question = "要创建采购草稿，请先告诉我商品名称或 SKU。",
                Reason = "purchase_missing_product"
            };
            return true;
        }

        if (!allowedCaps.Any(c => string.Equals(c.Code, "purchase.price.compare", StringComparison.OrdinalIgnoreCase)))
        {
            decision = BuildMissingSlotsClarify(session);
            return true;
        }

        var args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (hasProduct)
        {
            args["productId"] = productId!.Trim();
        }

        if (hasMaterial)
        {
            args["materialId"] = materialId!.Trim();
        }

        decision = new AgentPlanDecision
        {
            Action = "call_tool",
            Tool = "purchase.price.compare",
            Arguments = args,
            Reason = "purchase_need_price_compare"
        };
        return true;
    }

    private static bool TryBuildCreateOrMissing(
        AgentSession session,
        IReadOnlyList<AgentCapabilityDefinition> allowedCaps,
        out AgentPlanDecision decision)
    {
        if (!HasCreateReadySlots(session, out var missing))
        {
            decision = BuildMissingSlotsClarify(session, missing);
            return true;
        }

        var hasProduct = session.Slots.TryGetValue("productId", out var productId)
                         && !string.IsNullOrWhiteSpace(productId);
        var createCode = hasProduct ? "purchase.order.create.product" : "purchase.order.create.material";
        if (!allowedCaps.Any(c => string.Equals(c.Code, createCode, StringComparison.OrdinalIgnoreCase)))
        {
            decision = new AgentPlanDecision
            {
                Action = "clarify",
                Question = $"当前无权使用 `{createCode}`，请检查采购写入权限。",
                Reason = "purchase_capability_missing"
            };
            return true;
        }

        PendingTaskSnapshot.EnsureIdempotencyKeys(session);
        session.Slots.TryGetValue("purchaseQuantity", out var qty);
        if (string.IsNullOrWhiteSpace(qty))
        {
            session.Slots.TryGetValue("qty", out qty);
        }

        var createArgs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["supplierId"] = session.Slots["supplierId"].Trim(),
            ["qty"] = qty!.Trim(),
            ["uomId"] = session.Slots["uomId"].Trim(),
            ["unitPrice"] = session.Slots["unitPrice"].Trim(),
            ["idempotencyKey"] = session.Slots[PendingTaskKinds.SlotIdempotencyKey],
            ["lineIdempotencyKey"] = session.Slots[PendingTaskKinds.SlotLineIdempotencyKey]
        };
        if (hasProduct)
        {
            createArgs["productId"] = productId!.Trim();
        }
        else
        {
            createArgs["materialId"] = session.Slots["materialId"].Trim();
        }

        decision = new AgentPlanDecision
        {
            Action = "call_tool",
            Tool = createCode,
            Arguments = createArgs,
            Reason = "deterministic_purchase_create"
        };
        return true;
    }

    private static bool HasCreateReadySlots(AgentSession session, out List<string> missing)
    {
        missing = [];
        var hasProduct = session.Slots.TryGetValue("productId", out var pid) && !string.IsNullOrWhiteSpace(pid);
        var hasMaterial = session.Slots.TryGetValue("materialId", out var mid) && !string.IsNullOrWhiteSpace(mid);
        if (!hasProduct && !hasMaterial)
        {
            missing.Add("商品(productId/materialId)");
        }

        session.Slots.TryGetValue("purchaseQuantity", out var qty);
        if (string.IsNullOrWhiteSpace(qty))
        {
            session.Slots.TryGetValue("qty", out qty);
        }

        if (string.IsNullOrWhiteSpace(session.Slots.GetValueOrDefault("supplierId")))
        {
            missing.Add("供应商(supplierId)");
        }

        if (string.IsNullOrWhiteSpace(qty))
        {
            missing.Add("数量(qty)");
        }

        if (string.IsNullOrWhiteSpace(session.Slots.GetValueOrDefault("unitPrice")))
        {
            missing.Add("单价(unitPrice)");
        }

        if (string.IsNullOrWhiteSpace(session.Slots.GetValueOrDefault("uomId")))
        {
            missing.Add("单位(uomId)");
        }

        return missing.Count == 0;
    }

    private static AgentPlanDecision BuildMissingSlotsClarify(
        AgentSession session,
        List<string>? missing = null)
    {
        _ = HasCreateReadySlots(session, out var m);
        missing ??= m;
        session.Slots.TryGetValue("_resolved_product", out var name);
        var subject = string.IsNullOrWhiteSpace(name) ? "该商品" : name!.Trim();
        return new AgentPlanDecision
        {
            Action = "clarify",
            Question =
                $"采购待办（{subject}）还缺：{string.Join("、", missing)}。请补充，或回复序号选供应商；齐了后再说「确认创建」。",
            Reason = "purchase_missing_slots"
        };
    }

    private static AgentPlanDecision BuildFinalConfirmClarify(AgentSession session, string preface)
    {
        session.Slots.TryGetValue("_resolved_product", out var name);
        session.Slots.TryGetValue("_resolved_supplier", out var supplier);
        session.Slots.TryGetValue("supplierId", out var supplierId);
        session.Slots.TryGetValue("unitPrice", out var price);
        session.Slots.TryGetValue("purchaseQuantity", out var qty);
        if (string.IsNullOrWhiteSpace(qty))
        {
            session.Slots.TryGetValue("qty", out qty);
        }

        var subject = string.IsNullOrWhiteSpace(name) ? "该商品" : name!.Trim();
        var supplierText = !string.IsNullOrWhiteSpace(supplier)
            ? supplier
            : $"Id={supplierId}";
        return new AgentPlanDecision
        {
            Action = "clarify",
            Question =
                $"{preface}\n请最终确认创建采购草稿：{subject} × {qty}，供应商 {supplierText}，单价 {price}。回复「确认创建」或「取消」。",
            Reason = "purchase_await_final_confirm"
        };
    }
}
