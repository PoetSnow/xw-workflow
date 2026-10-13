using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XW.Agent.Application.Options;

namespace XW.Agent.Application.Llm;

/// <summary>
/// 运行时 LLM 接入：配置 Profiles 为种子，API 可切换/覆盖，可选落盘。
/// </summary>
public sealed class AgentLlmRuntime : IAgentLlmRuntime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly object _gate = new();
    private readonly IOptionsMonitor<AgentOptions> _options;
    private readonly ILogger<AgentLlmRuntime> _logger;
    private readonly string _runtimePath;

    private string? _activeProfileCode;
    private AgentLlmConnection? _override;

    public AgentLlmRuntime(
        IOptionsMonitor<AgentOptions> options,
        IHostEnvironment env,
        ILogger<AgentLlmRuntime> logger)
    {
        _options = options;
        _logger = logger;
        _runtimePath = Path.Combine(env.ContentRootPath, "agent-llm.runtime.json");
        LoadPersisted();
    }

    public bool Enabled => _options.CurrentValue.Llm.Enabled;
    public double ConfidenceThreshold => _options.CurrentValue.Llm.ConfidenceThreshold;
    public bool FallbackToRules => _options.CurrentValue.Llm.FallbackToRules;

    public AgentLlmConnection GetActive()
    {
        lock (_gate)
        {
            if (_override is not null)
            {
                return _override;
            }

            var llm = _options.CurrentValue.Llm;
            var code = _activeProfileCode ?? llm.ActiveProfile;
            var profile = FindProfile(llm, code) ?? llm.Profiles.FirstOrDefault()
                ?? throw new InvalidOperationException("No Agent LLM profile configured");

            return ToConnection(profile);
        }
    }

    public IReadOnlyList<AgentLlmProfileView> ListProfiles()
    {
        lock (_gate)
        {
            var active = GetActiveUnlocked();
            return _options.CurrentValue.Llm.Profiles.Select(p => new AgentLlmProfileView
            {
                Code = p.Code,
                Name = p.Name,
                ProviderCode = p.ProviderCode,
                Endpoint = p.Endpoint,
                ModelName = p.ModelName,
                HasApiKey = !string.IsNullOrWhiteSpace(p.ApiKey),
                UseJsonResponseFormat = p.UseJsonResponseFormat,
                ToolProtocol = AgentToolProtocols.Normalize(p.ToolProtocol),
                IsActive = string.Equals(p.Code, active.ProfileCode, StringComparison.OrdinalIgnoreCase)
                           && _override is null
            }).ToList();
        }
    }

    public void ActivateProfile(string profileCode)
    {
        if (string.IsNullOrWhiteSpace(profileCode))
        {
            throw new ArgumentException("profileCode is required", nameof(profileCode));
        }

        lock (_gate)
        {
            var profile = FindProfile(_options.CurrentValue.Llm, profileCode)
                ?? throw new InvalidOperationException($"LLM profile '{profileCode}' not found");

            _activeProfileCode = profile.Code;
            _override = null;
            PersistLocked();
            _logger.LogInformation("Agent LLM active profile -> {Code} ({Endpoint}/{Model})",
                profile.Code, profile.Endpoint, profile.ModelName);
        }
    }

    public void ApplyOverride(AgentLlmConnectionOverride o)
    {
        lock (_gate)
        {
            var baseConn = _override ?? GetActiveUnlocked();
            _override = new AgentLlmConnection
            {
                ProfileCode = "runtime",
                Name = string.IsNullOrWhiteSpace(o.Name) ? "运行时覆盖" : o.Name!,
                ProviderCode = string.IsNullOrWhiteSpace(o.ProviderCode) ? baseConn.ProviderCode : o.ProviderCode!,
                Endpoint = string.IsNullOrWhiteSpace(o.Endpoint) ? baseConn.Endpoint : o.Endpoint!.TrimEnd('/'),
                ModelName = string.IsNullOrWhiteSpace(o.ModelName) ? baseConn.ModelName : o.ModelName!,
                ApiKey = o.ApiKey ?? baseConn.ApiKey,
                Temperature = o.Temperature ?? baseConn.Temperature,
                UseJsonResponseFormat = o.UseJsonResponseFormat ?? baseConn.UseJsonResponseFormat,
                ToolProtocol = string.IsNullOrWhiteSpace(o.ToolProtocol)
                    ? baseConn.ToolProtocol
                    : AgentToolProtocols.Normalize(o.ToolProtocol),
                ChatCompletionsPath = string.IsNullOrWhiteSpace(o.ChatCompletionsPath)
                    ? baseConn.ChatCompletionsPath
                    : o.ChatCompletionsPath!
            };
            PersistLocked();
            _logger.LogInformation("Agent LLM runtime override -> {Endpoint}/{Model}",
                _override.Endpoint, _override.ModelName);
        }
    }

    public void ClearOverride()
    {
        lock (_gate)
        {
            _override = null;
            PersistLocked();
        }
    }

    private AgentLlmConnection GetActiveUnlocked()
    {
        if (_override is not null)
        {
            return _override;
        }

        var llm = _options.CurrentValue.Llm;
        var code = _activeProfileCode ?? llm.ActiveProfile;
        var profile = FindProfile(llm, code) ?? llm.Profiles.FirstOrDefault()
            ?? throw new InvalidOperationException("No Agent LLM profile configured");
        return ToConnection(profile);
    }

    private static AgentLlmProfileOptions? FindProfile(AgentLlmOptions llm, string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return llm.Profiles.FirstOrDefault(p =>
            string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    private static AgentLlmConnection ToConnection(AgentLlmProfileOptions p) => new()
    {
        ProfileCode = p.Code,
        Name = p.Name,
        ProviderCode = p.ProviderCode,
        Endpoint = (p.Endpoint ?? string.Empty).TrimEnd('/'),
        ModelName = p.ModelName,
        ApiKey = p.ApiKey ?? string.Empty,
        Temperature = p.Temperature,
        UseJsonResponseFormat = p.UseJsonResponseFormat,
        ToolProtocol = AgentToolProtocols.Normalize(p.ToolProtocol),
        ChatCompletionsPath = string.IsNullOrWhiteSpace(p.ChatCompletionsPath)
            ? "/v1/chat/completions"
            : p.ChatCompletionsPath
    };

    private void LoadPersisted()
    {
        try
        {
            if (!File.Exists(_runtimePath))
            {
                return;
            }

            var json = File.ReadAllText(_runtimePath);
            var state = JsonSerializer.Deserialize<RuntimeState>(json, JsonOptions);
            if (state is null)
            {
                return;
            }

            lock (_gate)
            {
                _activeProfileCode = state.ActiveProfileCode;
                if (state.Override is not null && !string.IsNullOrWhiteSpace(state.Override.Endpoint))
                {
                    _override = state.Override;
                }
            }

            _logger.LogInformation("Loaded Agent LLM runtime state from {Path}", _runtimePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load Agent LLM runtime state");
        }
    }

    private void PersistLocked()
    {
        if (!_options.CurrentValue.Llm.PersistRuntimeOverrides)
        {
            return;
        }

        try
        {
            var state = new RuntimeState
            {
                ActiveProfileCode = _activeProfileCode,
                Override = _override
            };
            File.WriteAllText(_runtimePath, JsonSerializer.Serialize(state, JsonOptions));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist Agent LLM runtime state");
        }
    }

    private sealed class RuntimeState
    {
        public string? ActiveProfileCode { get; set; }
        public AgentLlmConnection? Override { get; set; }
    }
}
