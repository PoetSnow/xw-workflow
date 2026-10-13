using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace XW.Agent.Application.Llm;

/// <summary>交给原生 tool call 的一条函数声明。Schema 由技能参数生成。</summary>
public sealed record AgentNativeToolSpec(string Name, string Description, string InputSchemaJson);

/// <summary>业务侧补全门面（Planner/Intent/Reply 不变）；底层走 <see cref="IChatClient"/>。</summary>
public interface IChatCompletionClient
{
    Task<string> CompleteJsonAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default);

    /// <summary>自然语言补全（不对 JSON 做强制约束）。</summary>
    Task<string> CompleteTextAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default);

    Task<AgentLlmProbeResult> ProbeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 OpenAI tools 协议要一次补全。返回值里可能有 FunctionCallContent。
    /// 不与 response_format=json_object 同时发送。探测不走这个方法。
    /// </summary>
    Task<ChatResponse> CompleteNativeToolsAsync(
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<AgentNativeToolSpec> tools,
        CancellationToken cancellationToken = default);
}

public sealed class AgentLlmProbeResult
{
    public bool Ok { get; init; }
    public string ProfileCode { get; init; } = string.Empty;
    public string Endpoint { get; init; } = string.Empty;
    public string ModelName { get; init; } = string.Empty;
    public string? Message { get; init; }
    public long ElapsedMs { get; init; }
}

/// <summary>
/// Sprint 2 Adapter：把 Agent 既有 CompleteJson/Text API 接到 Microsoft.Extensions.AI 的 IChatClient。
/// </summary>
public sealed class ChatCompletionClientAdapter(
    IChatClient chatClient,
    IAgentLlmRuntime runtime,
    ILogger<ChatCompletionClientAdapter> logger) : IChatCompletionClient
{
    public async Task<string> CompleteJsonAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        var text = await CompleteCoreAsync(systemPrompt, userPrompt, forceJson: true, cancellationToken);
        return ExtractJsonObject(text);
    }

    public Task<string> CompleteTextAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default) =>
        CompleteCoreAsync(systemPrompt, userPrompt, forceJson: false, cancellationToken);

    /// <inheritdoc />
    public async Task<ChatResponse> CompleteNativeToolsAsync(
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<AgentNativeToolSpec> tools,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt ?? string.Empty),
            new(ChatRole.User, userPrompt ?? string.Empty)
        };

        // Schema 文档必须活到 GetResponseAsync 结束，客户端序列化时还要读 JsonElement。
        var docs = new List<JsonDocument>();
        var declarations = new List<AITool>();
        try
        {
            foreach (var tool in tools)
            {
                var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(tool.InputSchemaJson)
                    ? """{"type":"object","properties":{}}"""
                    : tool.InputSchemaJson);
                docs.Add(doc);
                declarations.Add(AIFunctionFactory.CreateDeclaration(
                    tool.Name,
                    tool.Description ?? tool.Name,
                    doc.RootElement));
            }

            var options = new ChatOptions
            {
                Temperature = runtime.GetActive().Temperature,
                Tools = declarations
            };
            return await chatClient.GetResponseAsync(messages, options, cancellationToken);
        }
        finally
        {
            foreach (var doc in docs)
            {
                doc.Dispose();
            }
        }
    }

    public async Task<AgentLlmProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var conn = runtime.GetActive();
        logger.LogInformation(
            "LLM probe start profile={Profile} endpoint={Endpoint} model={Model}",
            conn.ProfileCode, conn.Endpoint, conn.ModelName);
        try
        {
            var json = await CompleteJsonAsync(
                "Return ONLY valid JSON: {\"ok\":true}",
                "ping",
                cancellationToken);
            sw.Stop();
            logger.LogInformation("LLM probe ok elapsedMs={Elapsed}", sw.ElapsedMilliseconds);
            return new AgentLlmProbeResult
            {
                Ok = true,
                ProfileCode = conn.ProfileCode,
                Endpoint = conn.Endpoint,
                ModelName = conn.ModelName,
                Message = Truncate(json, 200),
                ElapsedMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogWarning(ex, "LLM probe failed elapsedMs={Elapsed}", sw.ElapsedMilliseconds);
            return new AgentLlmProbeResult
            {
                Ok = false,
                ProfileCode = conn.ProfileCode,
                Endpoint = conn.Endpoint,
                ModelName = conn.ModelName,
                Message = ex.Message,
                ElapsedMs = sw.ElapsedMilliseconds
            };
        }
    }

    private async Task<string> CompleteCoreAsync(
        string systemPrompt,
        string userPrompt,
        bool forceJson,
        CancellationToken cancellationToken)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt ?? string.Empty),
            new(ChatRole.User, userPrompt ?? string.Empty)
        };

        var options = new ChatOptions
        {
            Temperature = forceJson
                ? runtime.GetActive().Temperature
                : Math.Clamp(runtime.GetActive().Temperature + 0.3f, 0.2f, 0.9f)
        };

        if (forceJson)
        {
            options.ResponseFormat = ChatResponseFormat.Json;
            options.AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["agent.force_json"] = "true"
            };
        }

        var response = await chatClient.GetResponseAsync(messages, options, cancellationToken);
        var text = response.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("LLM returned empty content");
        }

        return text.Trim();
    }

    private static string ExtractJsonObject(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith('{'))
        {
            return trimmed;
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            return trimmed[start..(end + 1)];
        }

        return trimmed;
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.Empty;
        }

        return s.Length <= max ? s : s[..max] + "...";
    }
}
