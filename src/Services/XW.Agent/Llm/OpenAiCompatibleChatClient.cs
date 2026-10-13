using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using XW.Agent.Application.Telemetry;

namespace XW.Agent.Application.Llm;

/// <summary>
/// Sprint 2：OpenAI 兼容端点的 <see cref="IChatClient"/> 实现（支持运行时 Profile 热切换）。
/// </summary>
public sealed class OpenAiCompatibleChatClient(
    HttpClient http,
    IAgentLlmRuntime runtime,
    IAgentTurnTelemetry turnTelemetry,
    ILogger<OpenAiCompatibleChatClient> logger) : IChatClient
{
    public const string HttpClientName = "agent-llm";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var callId = Guid.NewGuid().ToString("N")[..12];
        var conn = runtime.GetActive();
        if (!conn.IsReady)
        {
            logger.LogError(
                "LLM[{CallId}] skip: profile={Profile} endpoint/model not ready",
                callId, conn.ProfileCode);
            throw new InvalidOperationException("Agent LLM endpoint/model is not configured");
        }

        var messageList = messages.ToList();
        var toolDefs = ReadToolDefinitions(options);
        var forceJson = toolDefs.Count == 0 && (IsJsonResponse(options) || LooksLikeJsonRequest(messageList));
        var useJsonFormat = forceJson && (options?.ResponseFormat is ChatResponseFormatJson || conn.UseJsonResponseFormat);
        // 若 ChatOptions 显式要求 JSON，本地模型不支持 response_format 时仍尽量请求
        if (options?.ResponseFormat is ChatResponseFormatJson && conn.UseJsonResponseFormat && toolDefs.Count == 0)
        {
            useJsonFormat = true;
        }

        // 原生 tools 与 json_object 不同时发送，避免网关拒掉其中一边。
        var mode = toolDefs.Count > 0 ? "tools" : forceJson ? "json" : "text";
        var temperature = options?.Temperature is float t
            ? t
            : forceJson
                ? conn.Temperature
                : Math.Clamp(conn.Temperature + 0.3f, 0.2f, 0.9f);

        var model = !string.IsNullOrWhiteSpace(options?.ModelId) ? options!.ModelId! : conn.ModelName;
        var url = BuildUrl(conn);

        using var llmActivity = AgentTelemetry.StartLlm(conn.ProfileCode, model, mode);
        llmActivity?.SetTag("agent.llm.call_id", callId);
        llmActivity?.SetTag("gen_ai.system", "openai_compatible");
        llmActivity?.SetTag("gen_ai.request.model", model);

        logger.LogInformation(
            "LLM[{CallId}] IChatClient start mode={Mode} profile={Profile} provider={Provider} model={Model} url={Url} temp={Temp} jsonFormat={JsonFormat} msgs={MsgCount}",
            callId, mode, conn.ProfileCode, conn.ProviderCode, model, url, temperature, useJsonFormat, messageList.Count);

        var apiMessages = messageList.Select(m => new
        {
            role = ToOpenAiRole(m.Role),
            content = m.Text ?? string.Empty
        }).ToArray();

        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["temperature"] = temperature,
            ["messages"] = apiMessages
        };
        if (useJsonFormat)
        {
            payload["response_format"] = new { type = "json_object" };
        }

        if (toolDefs.Count > 0)
        {
            payload["tools"] = toolDefs;
            payload["tool_choice"] = "auto";
        }

        object body = payload;

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        if (!string.IsNullOrWhiteSpace(conn.ApiKey))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", conn.ApiKey);
        }

        req.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

        var sw = Stopwatch.StartNew();
        int? httpStatus = null;
        try
        {
            using var resp = await http.SendAsync(req, cancellationToken);
            var raw = await resp.Content.ReadAsStringAsync(cancellationToken);
            sw.Stop();
            httpStatus = (int)resp.StatusCode;
            llmActivity?.SetTag("agent.llm.elapsed_ms", sw.ElapsedMilliseconds);
            llmActivity?.SetTag("http.response.status_code", httpStatus);

            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "LLM[{CallId}] http failed status={Status} elapsedMs={Elapsed} body={Body}",
                    callId, (int)resp.StatusCode, sw.ElapsedMilliseconds, Truncate(raw, 800));
                AgentTelemetry.SetOk(llmActivity, false);
                throw new InvalidOperationException($"LLM call failed: {(int)resp.StatusCode}");
            }

            string? content;
            var toolCalls = new List<FunctionCallContent>();
            int promptTokens = 0, completionTokens = 0, totalTokens = 0;
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var message = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message");
                content = message.TryGetProperty("content", out var contentEl) && contentEl.ValueKind == JsonValueKind.String
                    ? contentEl.GetString()
                    : null;
                if (message.TryGetProperty("tool_calls", out var callsEl) && callsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var call in callsEl.EnumerateArray())
                    {
                        var callIdFromModel = call.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                        if (!call.TryGetProperty("function", out var functionEl))
                        {
                            continue;
                        }

                        var functionName = functionEl.TryGetProperty("name", out var nameEl)
                            ? nameEl.GetString()
                            : null;
                        if (string.IsNullOrWhiteSpace(functionName))
                        {
                            continue;
                        }

                        var argumentsJson = functionEl.TryGetProperty("arguments", out var argsEl)
                                            && argsEl.ValueKind == JsonValueKind.String
                            ? argsEl.GetString()
                            : "{}";
                        toolCalls.Add(new FunctionCallContent(
                            string.IsNullOrWhiteSpace(callIdFromModel) ? callId : callIdFromModel!,
                            functionName,
                            ParseToolArguments(argumentsJson)));
                    }
                }

                if (doc.RootElement.TryGetProperty("usage", out var usage))
                {
                    logger.LogInformation("LLM[{CallId}] usage {Usage}", callId, Truncate(usage.ToString(), 300));
                    if (usage.TryGetProperty("prompt_tokens", out var pt))
                    {
                        promptTokens = pt.GetInt32();
                    }

                    if (usage.TryGetProperty("completion_tokens", out var ctTok))
                    {
                        completionTokens = ctTok.GetInt32();
                    }

                    if (usage.TryGetProperty("total_tokens", out var tt))
                    {
                        totalTokens = tt.GetInt32();
                    }
                    else
                    {
                        totalTokens = promptTokens + completionTokens;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "LLM[{CallId}] parse failed raw={Raw}", callId, Truncate(raw, 800));
                throw new InvalidOperationException("LLM response parse failed", ex);
            }

            if (string.IsNullOrWhiteSpace(content) && toolCalls.Count == 0)
            {
                throw new InvalidOperationException("LLM returned empty content");
            }

            var trimmed = content?.Trim() ?? string.Empty;
            logger.LogInformation(
                "LLM[{CallId}] ok mode={Mode} elapsedMs={Elapsed} contentLen={Len} toolCalls={Tools} preview={Preview}",
                callId, mode, sw.ElapsedMilliseconds, trimmed.Length, toolCalls.Count, Truncate(trimmed, 400));
            AgentTelemetry.SetOk(llmActivity, true);

            turnTelemetry.AddLlmCall(new AgentLlmCallUsage
            {
                CallId = callId,
                Profile = conn.ProfileCode,
                Model = model,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                TotalTokens = totalTokens,
                ElapsedMs = sw.ElapsedMilliseconds
            });

            var contents = new List<AIContent>();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                contents.Add(new TextContent(trimmed));
            }

            contents.AddRange(toolCalls);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, contents))
            {
                ModelId = model,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    ["call_id"] = callId,
                    ["elapsed_ms"] = sw.ElapsedMilliseconds,
                    ["profile"] = conn.ProfileCode,
                    ["prompt_tokens"] = promptTokens,
                    ["completion_tokens"] = completionTokens,
                    ["total_tokens"] = totalTokens
                }
            };
        }
        catch (Exception ex)
        {
            AgentTelemetry.SetError(llmActivity, ex);
            if (ex is not OperationCanceledException)
            {
                AgentTelemetry.RecordLlmFailure(httpStatus);
            }

            throw;
        }
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 本地/兼容网关多数未开 stream；先非流式再一次推送，保证接口完备。
        var response = await GetResponseAsync(messages, options, cancellationToken);
        yield return new ChatResponseUpdate(ChatRole.Assistant, response.Text);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == typeof(IChatClient) || serviceType == typeof(OpenAiCompatibleChatClient))
        {
            return this;
        }

        if (serviceType == typeof(IAgentLlmRuntime))
        {
            return runtime;
        }

        return null;
    }

    public void Dispose()
    {
        // HttpClient 由 IHttpClientFactory 管理
    }

    private static List<object> ReadToolDefinitions(ChatOptions? options)
    {
        var list = new List<object>();
        if (options?.Tools is null)
        {
            return list;
        }

        foreach (var tool in options.Tools)
        {
            if (tool is not AIFunction function)
            {
                continue;
            }

            list.Add(new
            {
                type = "function",
                function = new
                {
                    name = function.Name,
                    description = function.Description,
                    parameters = function.JsonSchema.ValueKind == JsonValueKind.Undefined
                        ? JsonSerializer.Deserialize<JsonElement>("""{"type":"object","properties":{}}""")
                        : JsonSerializer.Deserialize<JsonElement>(function.JsonSchema.GetRawText())
                }
            });
        }

        return list;
    }

    private static Dictionary<string, object?> ParseToolArguments(string? argumentsJson)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return result;
        }

        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return result;
            }

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                result[property.Name] = property.Value.Clone();
            }
        }
        catch (JsonException)
        {
            // 参数不是 JSON 对象时交给规划器按空参数处理，不把原文当成功调用。
        }

        return result;
    }

    private static bool IsJsonResponse(ChatOptions? options) =>
        options?.ResponseFormat is ChatResponseFormatJson
        || string.Equals(options?.AdditionalProperties?.GetValueOrDefault("agent.force_json")?.ToString(), "true",
            StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeJsonRequest(IReadOnlyList<ChatMessage> messages)
    {
        var system = messages.FirstOrDefault(m => m.Role == ChatRole.System)?.Text ?? string.Empty;
        return system.Contains("只输出一个 JSON", StringComparison.Ordinal)
               || system.Contains("ONLY valid JSON", StringComparison.OrdinalIgnoreCase)
               || system.Contains("JSON 对象", StringComparison.Ordinal);
    }

    private static string ToOpenAiRole(ChatRole role)
    {
        if (role == ChatRole.System) return "system";
        if (role == ChatRole.Assistant) return "assistant";
        if (role == ChatRole.Tool) return "tool";
        return "user";
    }

    private static string BuildUrl(AgentLlmConnection conn)
    {
        var path = conn.ChatCompletionsPath.StartsWith('/')
            ? conn.ChatCompletionsPath
            : "/" + conn.ChatCompletionsPath;
        return conn.Endpoint.TrimEnd('/') + path;
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
