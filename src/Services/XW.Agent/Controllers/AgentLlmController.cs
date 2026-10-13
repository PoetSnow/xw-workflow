using Microsoft.AspNetCore.Mvc;
using XW.Agent.Application.Llm;

namespace XW.Agent.Application.Controllers;

/// <summary>LLM 接入运行时配置（本地/云端切换，不必改 appsettings 重启）。</summary>
[ApiController]
[Route("api/agent/llm")]
public sealed class AgentLlmController(IAgentLlmRuntime runtime, IChatCompletionClient llm) : ControllerBase
{
    [HttpGet]
    public ActionResult<object> GetActive() => Ok(BuildStatus());

    [HttpGet("profiles")]
    public ActionResult<IReadOnlyList<AgentLlmProfileView>> ListProfiles() =>
        Ok(runtime.ListProfiles());

    /// <summary>
    /// 切换 Profile，或直接覆盖 Endpoint/Model（本地热切换）。
    /// body 示例：{ "profileCode": "local" }
    /// 或：{ "override": { "endpoint": "http://127.0.0.1:11434", "modelName": "qwen2.5:14b" } }
    /// </summary>
    [HttpPut("active")]
    public IActionResult Activate([FromBody] AgentLlmActivateRequest request)
    {
        if (request.Override is not null
            && (!string.IsNullOrWhiteSpace(request.Override.Endpoint)
                || !string.IsNullOrWhiteSpace(request.Override.ModelName)
                || request.Override.ApiKey is not null
                || request.Override.UseJsonResponseFormat.HasValue
                || request.Override.Temperature.HasValue
                || !string.IsNullOrWhiteSpace(request.Override.ProviderCode)
                || !string.IsNullOrWhiteSpace(request.Override.ChatCompletionsPath)
                || !string.IsNullOrWhiteSpace(request.Override.ToolProtocol)))
        {
            runtime.ApplyOverride(request.Override);
            return Ok(BuildStatus());
        }

        if (!string.IsNullOrWhiteSpace(request.ProfileCode))
        {
            runtime.ActivateProfile(request.ProfileCode);
            return Ok(BuildStatus());
        }

        return BadRequest(new { message = "Provide profileCode or override" });
    }

    [HttpDelete("override")]
    public IActionResult ClearOverride()
    {
        runtime.ClearOverride();
        return Ok(BuildStatus());
    }

    /// <summary>探测当前 LLM 是否可通。</summary>
    [HttpPost("probe")]
    public async Task<ActionResult<AgentLlmProbeResult>> Probe(CancellationToken ct) =>
        Ok(await llm.ProbeAsync(ct));

    private object BuildStatus()
    {
        var c = runtime.GetActive();
        return new
        {
            enabled = runtime.Enabled,
            confidenceThreshold = runtime.ConfidenceThreshold,
            fallbackToRules = runtime.FallbackToRules,
            active = new
            {
                c.ProfileCode,
                c.Name,
                c.ProviderCode,
                c.Endpoint,
                c.ModelName,
                hasApiKey = !string.IsNullOrWhiteSpace(c.ApiKey),
                c.Temperature,
                c.UseJsonResponseFormat,
                c.ToolProtocol,
                c.ChatCompletionsPath,
                ready = c.IsReady
            }
        };
    }
}
