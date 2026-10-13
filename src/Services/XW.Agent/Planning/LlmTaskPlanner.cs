using System.Text.Json;
using Microsoft.Extensions.Logging;
using XW.Agent.Application.Llm;
using XW.Agent.Application.Models;

namespace XW.Agent.Application.Planning;

/// <summary>
/// Intent + Entity 层：一次 LLM JSON 抽取业务意图与槽位；不叠关键词。
/// Resolver / Capability 执行在下游。
/// </summary>
public sealed class LlmTaskPlanner(
    IChatCompletionClient llm,
    IAgentLlmRuntime llmRuntime,
    IPromptCatalog prompts,
    ILogger<LlmTaskPlanner> logger) : IAgentTaskPlanner
{
    private static readonly HashSet<string> AllowedIntents = new(StringComparer.OrdinalIgnoreCase)
    {
        "inventory_purchase_check",
        "inventory_query",
        "purchase_create",
        "agent_plan",
        "unknown"
    };

    /// <inheritdoc />
    public async Task<AgentTaskPlan> CreatePlanAsync(
        AgentSession session,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (!llmRuntime.Enabled || !llmRuntime.GetActive().IsReady)
        {
            throw new InvalidOperationException("llm_unavailable");
        }

        var history = string.Join(
            "\n",
            session.Messages.TakeLast(4).Select(m => $"{m.Role}: {Truncate(m.Content, 160)}"));

        var system = prompts.RenderTaskPlanSystem();
        var user = prompts.RenderTaskPlanUser(new Dictionary<string, string>
        {
            ["history"] = string.IsNullOrWhiteSpace(history) ? "(空)" : history,
            ["message"] = Truncate(message ?? string.Empty, 500)
        });

        var raw = await LlmTransientRetry.OnceAsync(
            token => llm.CompleteJsonAsync(system, user, token),
            cancellationToken);
        var plan = Parse(raw);
        logger.LogInformation(
            "TaskPlan(LLM) intent={Intent} conf={Conf:0.##} product={Product} reason={Reason}",
            plan.Intent,
            plan.Confidence,
            plan.Entities.GetValueOrDefault("product") ?? "-",
            plan.Reason ?? "-");
        return plan;
    }

    private static AgentTaskPlan Parse(string raw)
    {
        using var doc = JsonDocument.Parse(ExtractJsonObject(raw));
        var root = doc.RootElement;

        var intent = root.TryGetProperty("intent", out var intentEl)
            ? intentEl.GetString()?.Trim() ?? "unknown"
            : "unknown";
        if (!AllowedIntents.Contains(intent))
        {
            intent = "agent_plan";
        }

        var confidence = 0.6;
        if (root.TryGetProperty("confidence", out var confEl))
        {
            if (confEl.ValueKind == JsonValueKind.Number)
            {
                confidence = confEl.GetDouble();
            }
            else if (confEl.ValueKind == JsonValueKind.String
                     && double.TryParse(confEl.GetString(), out var c))
            {
                confidence = c;
            }
        }

        confidence = Math.Clamp(confidence, 0, 1);

        var entities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("entities", out var entEl) && entEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in entEl.EnumerateObject())
            {
                var v = p.Value.ValueKind switch
                {
                    JsonValueKind.String => p.Value.GetString(),
                    JsonValueKind.Number => p.Value.ToString(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => null
                };
                if (!string.IsNullOrWhiteSpace(v))
                {
                    entities[p.Name] = v.Trim();
                }
            }
        }

        // 壳子污染时丢掉，交给下游结构清洗或澄清
        if (entities.TryGetValue("product", out var product))
        {
            var cleaned = Entities.ProductNameNormalizer.Clean(product);
            if (string.IsNullOrWhiteSpace(cleaned) || LooksLikeShell(cleaned))
            {
                entities.Remove("product");
            }
            else
            {
                entities["product"] = cleaned;
            }
        }

        var reason = root.TryGetProperty("reason", out var r) ? r.GetString() : "llm_task_plan";

        return new AgentTaskPlan
        {
            Intent = intent,
            Entities = entities,
            SuggestedSteps = TaskPlanStepCatalog.ForIntent(intent),
            Confidence = confidence,
            Reason = reason ?? "llm_task_plan"
        };
    }

    private static bool LooksLikeShell(string product) =>
        product.Contains("库存", StringComparison.Ordinal)
        || product.Contains("少于", StringComparison.Ordinal)
        || product.Contains("采购", StringComparison.Ordinal)
        || product.StartsWith("看一下", StringComparison.Ordinal)
        || product.StartsWith("查一下", StringComparison.Ordinal);

    private static string ExtractJsonObject(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith('{'))
        {
            return trimmed;
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        return start >= 0 && end > start ? trimmed[start..(end + 1)] : trimmed;
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? string.Empty : s.Length <= max ? s : s[..max] + "...";
}

/// <summary>Intent → 建议步骤（第二阶段 Workflow 可直接消费）。</summary>
public static class TaskPlanStepCatalog
{
    public static IReadOnlyList<string> ForIntent(string intent) =>
        intent.ToLowerInvariant() switch
        {
            "inventory_purchase_check" =>
            [
                "resolve_product",
                "query_inventory",
                "check_stock",
                "create_purchase"
            ],
            "inventory_query" => ["resolve_product", "query_inventory"],
            "purchase_create" => ["resolve_product", "create_purchase"],
            _ => []
        };
}
