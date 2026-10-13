using System.Text.Json;
using Microsoft.Extensions.Logging;
using XW.Agent.Application.Models;
using XW.Agent.Application.Options;
using XW.Agent.Application.Tooling;

namespace XW.Agent.Application.Llm;

/// <summary>面向用户的自然语言回复。知识命中走知识模板，其余用工具事实。</summary>
public interface IReplyComposer
{
    Task<string> ComposeAsync(ReplyComposeRequest request, CancellationToken cancellationToken = default);
}

public sealed class ReplyComposeRequest
{
    public required AgentSession Session { get; init; }

    public required string UserMessage { get; init; }

    public required IntentResult Intent { get; init; }

    public required string Phase { get; init; }

    public string? ToolSummary { get; init; }

    public string? KnowledgeSummary { get; init; }

    public string? FallbackReply { get; init; }

    public IReadOnlyList<AgentCapabilityDefinition>? Capabilities { get; init; }
}

public sealed class LlmReplyComposer(
    IChatCompletionClient llm,
    IAgentLlmRuntime llmRuntime,
    IPromptCatalog prompts,
    ILogger<LlmReplyComposer> logger) : IReplyComposer
{
    public async Task<string> ComposeAsync(ReplyComposeRequest request, CancellationToken cancellationToken = default)
    {
        var fallback = string.IsNullOrWhiteSpace(request.FallbackReply)
            ? "好的，我已收到。"
            : request.FallbackReply!;

        if (!llmRuntime.Enabled || !llmRuntime.GetActive().IsReady)
        {
            return fallback;
        }

        try
        {
            // 鉴权/工具失败：禁止再走 LLM，否则小模型会编造「库存 100」
            if (ShouldUseDeterministicReply(request.Phase))
            {
                logger.LogInformation(
                    "Deterministic reply phase={Phase} (skip LLM to avoid hallucination)",
                    request.Phase);
                return fallback;
            }

            var knowledgeLane = IsKnowledgeLane(request);
            string system;
            string user;

            if (knowledgeLane)
            {
                // Knowledge：精简历史，把 token 留给片段（本地模型 context 常仅 2048）
                var history = string.Join("\n", request.Session.Messages.TakeLast(2).Select(m =>
                    $"{m.Role}: {(m.Content.Length <= 120 ? m.Content : m.Content[..120] + "...")}"));
                system = prompts.RenderKnowledgeReplySystem();
                user = prompts.RenderKnowledgeReplyUser(new Dictionary<string, string>
                {
                    ["phase"] = request.Phase,
                    ["knowledge"] = string.IsNullOrWhiteSpace(request.KnowledgeSummary)
                        ? "(无命中知识)"
                        : request.KnowledgeSummary!,
                    ["fallback"] = request.FallbackReply ?? "(无)",
                    ["history"] = string.IsNullOrWhiteSpace(history) ? "(空)" : history,
                    ["message"] = request.UserMessage
                });
            }
            else
            {
                var history = string.Join("\n", request.Session.Messages
                    .TakeLast(8)
                    .Select(m => $"{m.Role}: {m.Content}"));
                var caps = request.Capabilities is { Count: > 0 }
                    ? string.Join("\n", request.Capabilities.Take(12).Select(c =>
                        $"- {c.Name} / {c.Code}：{Truncate(c.Description, 80)}"))
                    : "- 查库存 / 查订单履约 / 低库存预警";

                system = prompts.RenderReplySystem();
                user = prompts.RenderReplyUser(new Dictionary<string, string>
                {
                    ["capabilities"] = caps,
                    ["phase"] = request.Phase,
                    ["intent"] = request.Intent.Intent,
                    ["confidence"] = request.Intent.Confidence.ToString("0.##"),
                    ["slots_json"] = JsonSerializer.Serialize(request.Intent.Slots),
                    ["missing_slots"] = request.Intent.MissingSlots.Count == 0
                        ? "(无)"
                        : string.Join(", ", request.Intent.MissingSlots),
                    ["tool_summary"] = request.ToolSummary ?? "(无)",
                    ["fallback"] = request.FallbackReply ?? "(无)",
                    ["history"] = string.IsNullOrWhiteSpace(history) ? "(空)" : history,
                    ["message"] = request.UserMessage
                });
            }

            var text = await LlmTransientRetry.OnceAsync(
                token => llm.CompleteTextAsync(system, user, token),
                cancellationToken);
            return string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "LLM reply compose failed; using fallback");
            return fallback;
        }
    }

    private static bool IsKnowledgeLane(ReplyComposeRequest request) =>
        string.Equals(request.Intent.Intent, "knowledge.answer", StringComparison.OrdinalIgnoreCase)
        || string.Equals(request.Intent.Intent, "knowledge.search", StringComparison.OrdinalIgnoreCase)
        || request.Phase.StartsWith("knowledge_", StringComparison.OrdinalIgnoreCase);

    private static bool ShouldUseDeterministicReply(string phase) =>
        phase is "auth_failed" or "tool_error" or "retry_blocked" or "stub_blocked"
            or "denied" or "clarify" or "empty_result" or "approval"
            or "knowledge_miss"
            or "tool_ok";

    private static string Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? string.Empty : s!.Length <= max ? s : s[..max] + "...";
}
