using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using XW.Agent.Application.Entities;
using XW.Agent.Application.Llm;
using XW.Agent.Application.Models;

namespace XW.Agent.Application.Planning;

/// <summary>
/// Intent+Entity：优先 LLM；不可用或结果过弱时用结构句式兜底（非同义词堆叠）。
/// </summary>
public sealed class CompositeTaskPlanner(
    LlmTaskPlanner llmPlanner,
    RuleBasedTaskPlanner rulePlanner,
    IAgentLlmRuntime llmRuntime,
    ILogger<CompositeTaskPlanner> logger) : IAgentTaskPlanner
{
    /// <inheritdoc />
    public async Task<AgentTaskPlan> CreatePlanAsync(
        AgentSession session,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (llmRuntime.Enabled && llmRuntime.GetActive().IsReady)
        {
            try
            {
                var llmPlan = await llmPlanner.CreatePlanAsync(session, message, cancellationToken);
                if (IsUsable(llmPlan))
                {
                    return llmPlan;
                }

                logger.LogInformation(
                    "TaskPlan LLM weak intent={Intent} conf={Conf:0.##} → rule fallback",
                    llmPlan.Intent, llmPlan.Confidence);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "TaskPlan LLM failed → rule fallback");
            }
        }

        return await rulePlanner.CreatePlanAsync(session, message, cancellationToken);
    }

    private static bool IsUsable(AgentTaskPlan plan)
    {
        if (string.Equals(plan.Intent, "unknown", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (plan.Confidence < 0.45)
        {
            return false;
        }

        // 库存/采购类必须抽出 product，否则算弱结果
        if (plan.Intent is "inventory_purchase_check" or "inventory_query" or "purchase_create")
        {
            return plan.Entities.TryGetValue("product", out var p) && !string.IsNullOrWhiteSpace(p);
        }

        return true;
    }
}

/// <summary>
/// 结构句式兜底：用「库存/采购/阈值」句式与数量正则抽槽，不维护口语同义词表。
/// </summary>
public sealed class RuleBasedTaskPlanner(ILogger<RuleBasedTaskPlanner> logger) : IAgentTaskPlanner
{
    /// <summary>结构句式：可选祈使壳 + 品名 + 可选「还有多少」+ 库存。</summary>
    private static readonly Regex StockProductPattern = new(
        @"^(?:请|帮我|麻烦)?(?:看|查|查询)?(?:一下|下)?\s*(?<product>.+?)\s*(?:还有)?(?:多少|数量)?\s*库存",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ThresholdPattern = new(
        @"(?:少于|低于|不足|小于)\s*(?<n>\d+(?:\.\d+)?)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex PurchaseQtyPattern = new(
        @"采购\s*(?<n>\d+(?:\.\d+)?)\s*(?:个|件|台|箱|套|批)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <inheritdoc />
    public Task<AgentTaskPlan> CreatePlanAsync(
        AgentSession session,
        string message,
        CancellationToken cancellationToken = default)
    {
        var text = (message ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return Task.FromResult(new AgentTaskPlan
            {
                Intent = "unknown",
                Confidence = 0.2,
                Reason = "empty_message"
            });
        }

        var hasStock = text.Contains("库存", StringComparison.Ordinal);
        var hasPurchase = text.Contains("采购", StringComparison.Ordinal);
        var hasThreshold = ThresholdPattern.IsMatch(text);

        var entities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var product = ExtractProductByStructure(text);
        if (!string.IsNullOrWhiteSpace(product))
        {
            entities["product"] = product;
        }

        if (hasStock && hasPurchase && hasThreshold)
        {
            FillThresholdAndQty(text, entities);
            var plan = new AgentTaskPlan
            {
                Intent = "inventory_purchase_check",
                Entities = entities,
                SuggestedSteps = TaskPlanStepCatalog.ForIntent("inventory_purchase_check"),
                Confidence = entities.ContainsKey("product") ? 0.7 : 0.45,
                Reason = "rule_structure_inventory_purchase_check"
            };
            logger.LogInformation(
                "TaskPlan(rule) intent={Intent} product={Product}",
                plan.Intent, entities.GetValueOrDefault("product") ?? "-");
            return Task.FromResult(plan);
        }

        if (hasStock)
        {
            return Task.FromResult(new AgentTaskPlan
            {
                Intent = "inventory_query",
                Entities = entities,
                SuggestedSteps = TaskPlanStepCatalog.ForIntent("inventory_query"),
                Confidence = entities.ContainsKey("product") ? 0.7 : 0.4,
                Reason = "rule_structure_inventory_query"
            });
        }

        if (hasPurchase)
        {
            FillThresholdAndQty(text, entities);
            return Task.FromResult(new AgentTaskPlan
            {
                Intent = "purchase_create",
                Entities = entities,
                SuggestedSteps = TaskPlanStepCatalog.ForIntent("purchase_create"),
                Confidence = entities.ContainsKey("product") ? 0.65 : 0.4,
                Reason = "rule_structure_purchase_create"
            });
        }

        return Task.FromResult(new AgentTaskPlan
        {
            Intent = "agent_plan",
            Entities = entities,
            SuggestedSteps = [],
            Confidence = 0.35,
            Reason = "rule_generic"
        });
    }

    private static string? ExtractProductByStructure(string text)
    {
        var m = StockProductPattern.Match(text);
        if (m.Success)
        {
            var cleaned = ProductNameNormalizer.Clean(m.Groups["product"].Value);
            if (!string.IsNullOrWhiteSpace(cleaned))
            {
                return cleaned;
            }
        }

        return ProductNameNormalizer.Extract(text);
    }

    private static void FillThresholdAndQty(string text, Dictionary<string, string> entities)
    {
        var thr = ThresholdPattern.Match(text);
        if (thr.Success
            && decimal.TryParse(thr.Groups["n"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
        {
            entities["minStock"] = thr.Groups["n"].Value;
        }

        var buy = PurchaseQtyPattern.Match(text);
        if (buy.Success
            && decimal.TryParse(buy.Groups["n"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
        {
            entities["purchaseQuantity"] = buy.Groups["n"].Value;
        }
    }
}
