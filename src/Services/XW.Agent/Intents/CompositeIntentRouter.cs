using Microsoft.Extensions.Logging;
using XW.Agent.Application.Abstractions;
using XW.Agent.Application.Llm;
using XW.Agent.Application.Models;

namespace XW.Agent.Application.Intents;

/// <summary>
/// LLM 主路径；失败或未就绪时可选回退规则路由。
/// 本地模型：有 Endpoint+Model 即视为已配置，ApiKey 可空。
/// </summary>
public sealed class CompositeIntentRouter(
    LlmIntentRouter llmRouter,
    RuleBasedIntentRouter ruleRouter,
    IAgentLlmRuntime llmRuntime,
    ILogger<CompositeIntentRouter> logger) : IIntentRouter
{
    public async Task<IntentResult> RouteAsync(AgentSession session, string message, CancellationToken ct = default)
    {
        var llmReady = llmRuntime.Enabled && llmRuntime.GetActive().IsReady;
        if (llmReady)
        {
            try
            {
                return await llmRouter.RouteAsync(session, message, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "LLM intent classification failed");
                if (!llmRuntime.FallbackToRules)
                {
                    return new IntentResult
                    {
                        Intent = AgentIntents.Unknown,
                        Confidence = 0,
                        ReplyHint = "意图识别暂时不可用，请稍后再试或检查 LLM 接入（/api/agent/llm）。"
                    };
                }
            }
        }
        else if (llmRuntime.Enabled)
        {
            logger.LogDebug("Agent LLM enabled but endpoint/model not ready; fallback={Fallback}",
                llmRuntime.FallbackToRules);
        }

        if (!llmRuntime.Enabled || llmRuntime.FallbackToRules || !llmReady)
        {
            return await ruleRouter.RouteAsync(session, message, ct);
        }

        return new IntentResult
        {
            Intent = AgentIntents.Unknown,
            Confidence = 0,
            ReplyHint = "请先配置 LLM 接入：PUT /api/agent/llm/active（本地可只填 Endpoint + ModelName）。"
        };
    }
}
