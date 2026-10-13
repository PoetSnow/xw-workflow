using XW.Agent.Application.Options;

namespace XW.Agent.Application.Llm;

/// <summary>当前生效的 LLM 连接（每次调用实时解析，支持热切换）。</summary>
public sealed class AgentLlmConnection
{
    public required string ProfileCode { get; init; }
    public required string Name { get; init; }
    public required string ProviderCode { get; init; }
    public required string Endpoint { get; init; }
    public required string ModelName { get; init; }
    public string ApiKey { get; init; } = string.Empty;
    public float Temperature { get; init; } = 0.1f;
    public bool UseJsonResponseFormat { get; init; }

    /// <summary>json_decision 或 native_tools。空值按 json_decision。</summary>
    public string ToolProtocol { get; init; } = AgentToolProtocols.JsonDecision;

    public string ChatCompletionsPath { get; init; } = "/v1/chat/completions";

    /// <summary>Endpoint 与 ModelName 均非空时视为可调用。</summary>
    public bool IsReady =>
        !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(ModelName);
}

/// <summary>LLM Profile 列表项（对外 API，不含完整 ApiKey）。</summary>
public sealed class AgentLlmProfileView
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string ProviderCode { get; init; }
    public required string Endpoint { get; init; }
    public required string ModelName { get; init; }
    public bool HasApiKey { get; init; }
    public bool UseJsonResponseFormat { get; init; }

    /// <summary>该 Profile 的工具协议。</summary>
    public string ToolProtocol { get; init; } = AgentToolProtocols.JsonDecision;

    public bool IsActive { get; init; }
}

/// <summary>激活 Profile 或临时覆盖连接的请求体。</summary>
public sealed class AgentLlmActivateRequest
{
    /// <summary>切换到已有 Profile Code；与 Override 二选一。</summary>
    public string? ProfileCode { get; set; }

    /// <summary>直接覆盖当前连接（不必先写进 Profiles）。</summary>
    public AgentLlmConnectionOverride? Override { get; set; }
}

/// <summary>运行时连接覆盖字段（未填保留原值）。</summary>
public sealed class AgentLlmConnectionOverride
{
    public string? Name { get; set; }
    public string? ProviderCode { get; set; }
    public string? Endpoint { get; set; }
    public string? ModelName { get; set; }
    public string? ApiKey { get; set; }
    public float? Temperature { get; set; }
    public bool? UseJsonResponseFormat { get; set; }

    /// <summary>未填则保留当前连接的协议。</summary>
    public string? ToolProtocol { get; set; }

    public string? ChatCompletionsPath { get; set; }
}

/// <summary>LLM 运行时：读配置、热切换 ActiveProfile、可选临时 Override。</summary>
public interface IAgentLlmRuntime
{
    /// <summary>总开关。</summary>
    bool Enabled { get; }

    /// <summary>意图置信度阈值。</summary>
    double ConfidenceThreshold { get; }

    /// <summary>LLM 失败时是否回退规则路由。</summary>
    bool FallbackToRules { get; }

    /// <summary>解析当前生效连接。</summary>
    AgentLlmConnection GetActive();

    /// <summary>列出全部 Profile（脱敏）。</summary>
    IReadOnlyList<AgentLlmProfileView> ListProfiles();

    /// <summary>切换 ActiveProfile。</summary>
    void ActivateProfile(string profileCode);

    /// <summary>应用临时连接覆盖。</summary>
    void ApplyOverride(AgentLlmConnectionOverride overrideSettings);

    /// <summary>清除临时覆盖，回到 ActiveProfile。</summary>
    void ClearOverride();
}
