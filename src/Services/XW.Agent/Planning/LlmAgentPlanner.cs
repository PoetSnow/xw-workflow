using System.Text.Json;
using Microsoft.Extensions.Logging;
using XW.Agent.Application.Capability;
using XW.Agent.Application.Llm;
using XW.Agent.Application.Models;
using XW.Agent.Application.Options;

namespace XW.Agent.Application.Planning;

/// <summary>
/// 根据「接口文档」（Capability 目录）+ 历史观察结果，由 LLM 决定下一步：调哪个 API / 追问 / 确认 / 结束。
/// </summary>
public sealed class LlmAgentPlanner(
    IChatCompletionClient llm,
    IAgentLlmRuntime llmRuntime,
    IPromptCatalog prompts,
    ILogger<LlmAgentPlanner> logger) : IAgentPlanner
{
    /// <inheritdoc />
    public async Task<AgentPlanDecision> NextAsync(
        AgentSession session,
        string userMessage,
        IReadOnlyList<AgentCapabilityDefinition> tools,
        IReadOnlyList<AgentPlanObservation> observations,
        string? knowledgeContext = null,
        CancellationToken ct = default)
    {
        // 知识片段已由 knowledge.search 写入观察；此处保留参数兼容
        _ = knowledgeContext;

        if (!llmRuntime.Enabled || !llmRuntime.GetActive().IsReady)
        {
            return new AgentPlanDecision
            {
                Action = "clarify",
                Question = "当前 LLM 未就绪，无法自动编排。请稍后再试，或联系管理员检查 Agent:Llm 配置。",
                Reason = "llm_unavailable"
            };
        }

        try
        {
            var obsText = observations.Count == 0
                ? "(尚无调用结果)"
                : string.Join("\n---\n", observations.Select((o, i) =>
                {
                    var args = o.Arguments is { Count: > 0 }
                        ? string.Join(", ", o.Arguments.Select(kv => $"{kv.Key}={kv.Value}"))
                        : "-";
                    var http = o.HttpStatusCode?.ToString() ?? "-";
                    var kind = o.ErrorKind ?? "-";
                    return
                        $"#{i + 1} tool={o.Tool} success={o.Success} http_status={http} error_kind={kind}\n" +
                        $"arguments={args}\nsummary={Truncate(o.Summary, 400)}\nerror={o.Error ?? "-"}";
                }));

            var history = string.Join("\n", session.Messages.TakeLast(4).Select(m => $"{m.Role}: {Truncate(m.Content, 200)}"));

            // Skill 循环：候选里含知识检索与业务能力
            var system = prompts.RenderPlannerSystem();
            var user = prompts.RenderPlannerUser(new Dictionary<string, string>
            {
                // 工具目录完整送出，不按字数截断，避免后面的 Schema 被裁掉。
                ["tools_json"] = BuildToolsJson(tools),
                ["observations"] = Truncate(obsText, 900),
                ["history"] = string.IsNullOrWhiteSpace(history) ? "(空)" : history,
                ["message"] = Truncate(userMessage, 400),
                ["confirmed"] = (session.Slots.GetValueOrDefault("_confirmed") ?? "false")
            });

            // 协议只影响这一次补全怎么要决策。返回值仍是 AgentPlanDecision。
            AgentPlanDecision decision;
            if (AgentToolProtocols.IsNative(llmRuntime.GetActive().ToolProtocol))
            {
                var specs = tools.Select(t => new AgentNativeToolSpec(
                    t.Code,
                    string.IsNullOrWhiteSpace(t.WhenToUse) ? t.Description : t.WhenToUse,
                    AgentCapabilitySchema.BuildInputSchema(t).GetRawText())).ToList();
                var response = await LlmTransientRetry.OnceAsync(
                    token => llm.CompleteNativeToolsAsync(system, user, specs, token),
                    ct);
                decision = NativeToolDecisionParser.FromResponse(response, tools)
                           ?? Parse(response.Text ?? string.Empty, tools);
            }
            else
            {
                var raw = await LlmTransientRetry.OnceAsync(
                    token => llm.CompleteJsonAsync(system, user, token),
                    ct);
                decision = Parse(raw, tools);
            }
            logger.LogInformation(
                "Planner action={Action} tool={Tool} reason={Reason}",
                decision.Action, decision.Tool ?? "-", decision.Reason ?? "-");
            return decision;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Planner failed");
            return new AgentPlanDecision
            {
                Action = "clarify",
                Question = "编排规划暂时失败，请稍后再试或换一种说法说明你的需求。",
                Reason = "planner_error"
            };
        }
    }

    /// <summary>
    /// 规划 prompt 里的工具目录。含输出提示和由参数生成的 input_schema。
    /// 整段 JSON 原样送出，不截断。
    /// </summary>
    internal static string BuildToolsJson(IReadOnlyList<AgentCapabilityDefinition> tools)
    {
        var catalog = tools.Select(t => new
        {
            code = t.Code,
            name = t.Name,
            purpose = string.IsNullOrWhiteSpace(t.WhenToUse) ? t.Description : t.WhenToUse,
            domain = t.Domain,
            confirm_required = t.ConfirmRequired,
            parameters = t.Parameters.Select(p => new
            {
                name = p.Name,
                required = p.Required,
                type = p.Type,
                description = p.Description
            }),
            output_hints = t.OutputHints
                .Where(h => !string.IsNullOrWhiteSpace(h.Name))
                .Select(h => new
                {
                    name = h.Name,
                    description = h.Description
                }),
            input_schema = AgentCapabilitySchema.BuildInputSchema(t)
        });

        return JsonSerializer.Serialize(catalog);
    }

    private static AgentPlanDecision Parse(string raw, IReadOnlyList<AgentCapabilityDefinition> tools)
    {
        using var doc = JsonDocument.Parse(ExtractJsonObject(raw));
        var root = doc.RootElement;
        var action = root.TryGetProperty("action", out var a)
            ? a.GetString()?.Trim().ToLowerInvariant() ?? "clarify"
            : "clarify";

        var tool = root.TryGetProperty("tool", out var t) ? t.GetString()?.Trim() : null;
        var question = root.TryGetProperty("question", out var q) ? q.GetString() : null;
        var reason = root.TryGetProperty("reason", out var r) ? r.GetString() : null;

        var args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("arguments", out var argsEl) && argsEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in argsEl.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.Null)
                {
                    continue;
                }

                var v = p.Value.ValueKind == JsonValueKind.String
                    ? p.Value.GetString()
                    : p.Value.ToString();
                if (!string.IsNullOrWhiteSpace(v))
                {
                    args[p.Name] = v!;
                }
            }
        }

        if (action is "call_tool" or "call" or "tool")
        {
            action = "call_tool";
            if (string.IsNullOrWhiteSpace(tool)
                || tools.All(x => !string.Equals(x.Code, tool, StringComparison.OrdinalIgnoreCase)))
            {
                return new AgentPlanDecision
                {
                    Action = "clarify",
                    Question = "我需要调用的接口不在可用目录中。请换一种业务描述，或联系管理员补充 Capability。",
                    Reason = "unknown_tool"
                };
            }
        }
        else if (action is "ask" or "clarify" or "clarification")
        {
            action = "clarify";
            if (string.IsNullOrWhiteSpace(question))
            {
                question = "请再补充一点信息，例如商品 SKU、品名或订单号。";
            }
        }
        else if (action is "confirm" or "approval")
        {
            action = "confirm";
            if (string.IsNullOrWhiteSpace(question))
            {
                question = "该操作需要你确认后才能继续，是否继续？";
            }
        }
        else
        {
            action = "finish";
        }

        return new AgentPlanDecision
        {
            Action = action,
            Tool = tool,
            Arguments = args,
            Question = question,
            Reason = reason
        };
    }

    private static string ExtractJsonObject(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return """{"action":"clarify","question":"请再说一次你的需求。"}""";
        }

        var s = raw.Trim();
        if (s.StartsWith("```", StringComparison.Ordinal))
        {
            var start = s.IndexOf('{');
            var end = s.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                return s[start..(end + 1)];
            }
        }

        var i = s.IndexOf('{');
        var j = s.LastIndexOf('}');
        if (i >= 0 && j > i)
        {
            return s[i..(j + 1)];
        }

        return s;
    }

    private static string Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? string.Empty : s.Length <= max ? s : s[..max] + "...";
}
