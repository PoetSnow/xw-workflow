using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XW.Agent.Application.Abstractions;
using XW.Agent.Application.Llm;
using XW.Agent.Application.Models;
using XW.Agent.Application.Options;
using XW.Agent.Application.Tooling;

namespace XW.Agent.Application.Intents;

internal sealed class LlmIntentClassificationDto
{
    [JsonPropertyName("intent")]
    public string Intent { get; set; } = "unknown";

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("slots")]
    public Dictionary<string, string>? Slots { get; set; }

    [JsonPropertyName("needs_clarification")]
    public bool NeedsClarification { get; set; }

    [JsonPropertyName("missing_slots")]
    public List<string>? MissingSlots { get; set; }

    [JsonPropertyName("reply_hint")]
    public string? ReplyHint { get; set; }
}

/// <summary>主路径：配置 Prompt + 意图目录 → LLM 结构化 Intent/Slots。</summary>
public sealed class LlmIntentRouter(
    IChatCompletionClient llm,
    IPromptCatalog prompts,
    IOptionsMonitor<AgentOptions> options,
    ICapabilityCatalog capabilities,
    ILogger<LlmIntentRouter> logger) : IIntentRouter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IntentResult> RouteAsync(AgentSession session, string message, CancellationToken ct = default)
    {
        var opt = options.CurrentValue;
        var catalogJson = JsonSerializer.Serialize(
            capabilities.ListEnabled().Select(c => new
            {
                intent = string.IsNullOrWhiteSpace(c.IntentCode) ? c.Code : c.IntentCode,
                tool = c.Code,
                description = c.Description,
                parameters = c.Parameters
            }).Concat(opt.Intents
                .Where(i => string.Equals(i.Code, "chitchat", StringComparison.OrdinalIgnoreCase)
                            || string.IsNullOrWhiteSpace(i.ToolName))
                .Select(i => new
                {
                    intent = i.Code,
                    tool = i.ToolName ?? "",
                    description = i.Description,
                    parameters = i.Slots.Select(s => new AgentCapabilityParameter
                    {
                        Name = s.Name,
                        Description = s.Description,
                        Required = s.Required
                    }).ToList()
                })),
            JsonOptions);

        var history = string.Join("\n", session.Messages.TakeLast(6).Select(m => $"{m.Role}: {m.Content}"));
        var systemPrompt = prompts.RenderIntentSystem();
        var userPrompt = prompts.RenderIntentUser(new Dictionary<string, string>
        {
            ["catalog_json"] = catalogJson,
            ["prior_slots_json"] = JsonSerializer.Serialize(session.Slots, JsonOptions),
            ["history"] = string.IsNullOrWhiteSpace(history) ? "(空)" : history,
            ["message"] = message
        });

        var json = await llm.CompleteJsonAsync(systemPrompt, userPrompt, ct);
        logger.LogDebug("LLM intent raw: {Json}", json);

        var parsed = JsonSerializer.Deserialize<LlmIntentClassificationDto>(json, JsonOptions)
            ?? throw new InvalidOperationException("LLM intent payload invalid");

        if (string.IsNullOrWhiteSpace(parsed.Intent))
        {
            throw new InvalidOperationException("LLM intent code empty");
        }

        var catalog = opt.Intents.FirstOrDefault(i =>
            string.Equals(i.Code, parsed.Intent, StringComparison.OrdinalIgnoreCase));

        var mergedSlots = new Dictionary<string, string>(session.Slots, StringComparer.OrdinalIgnoreCase);
        if (parsed.Slots is not null)
        {
            foreach (var (k, v) in parsed.Slots)
            {
                if (string.Equals(k, SessionCorpus.SlotKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(v))
                {
                    mergedSlots[k] = v.Trim();
                }
            }
        }

        foreach (var (k, v) in mergedSlots)
        {
            session.Slots[k] = v;
        }

        var missing = new List<string>();
        var requiredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (catalog is not null)
        {
            foreach (var slot in catalog.Slots.Where(s => s.Required))
            {
                requiredNames.Add(slot.Name);
                if (!mergedSlots.TryGetValue(slot.Name, out var val) || string.IsNullOrWhiteSpace(val))
                {
                    missing.Add(slot.Name);
                }
            }
        }

        if (parsed.MissingSlots is { Count: > 0 })
        {
            foreach (var s in parsed.MissingSlots.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                if (requiredNames.Contains(s)
                    && !missing.Contains(s, StringComparer.OrdinalIgnoreCase)
                    && (!mergedSlots.TryGetValue(s, out var val) || string.IsNullOrWhiteSpace(val)))
                {
                    missing.Add(s);
                }
            }
        }

        var needsClarification = missing.Count > 0 || parsed.NeedsClarification;
        return new IntentResult
        {
            Intent = parsed.Intent.Trim(),
            Confidence = parsed.Confidence,
            Slots = mergedSlots,
            MissingSlots = missing,
            ForceClarification = needsClarification && missing.Count == 0,
            ReplyHint = parsed.ReplyHint
        };
    }
}
