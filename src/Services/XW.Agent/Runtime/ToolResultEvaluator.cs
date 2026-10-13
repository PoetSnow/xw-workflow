using XW.Agent.Application.Models;
using XW.Agent.Application.Planning;

namespace XW.Agent.Application.Runtime;

/// <summary>工具/工作流结果裁定（规则，不调 LLM）。</summary>
public enum ToolResultKind
{
    /// <summary>有可用业务事实，可进 Reply LLM。</summary>
    Success = 0,

    /// <summary>鉴权失败。</summary>
    AuthFailed = 1,

    /// <summary>业务空结果，应追问/说明未找到。</summary>
    Empty = 2,

    /// <summary>执行/赋值等失败。</summary>
    Error = 3,

    /// <summary>Stub/演示数据不可信。</summary>
    Stub = 4
}

/// <summary>Evaluator 输出：相位 + 给用户的确定性文案。</summary>
public sealed class ToolResultEvaluation
{
    public required ToolResultKind Kind { get; init; }

    /// <summary>运行相位：tool_ok / auth_failed / clarify / tool_error / stub_blocked。</summary>
    public required string Phase { get; init; }

    /// <summary>确定性回复（失败/空结果时 Reply 层应直接使用，禁止 LLM 编造）。</summary>
    public required string UserMessage { get; init; }

    /// <summary>是否允许 Reply LLM 润色（仅 Success）。</summary>
    public bool AllowLlmReply => Kind == ToolResultKind.Success;
}

/// <summary>
/// Tool Result Evaluator：在 Planner/Executor 之后裁定成功/失败/空结果。
/// 防止「工具失败仍进 LLM 编造库存」。
/// </summary>
public interface IToolResultEvaluator
{
    ToolResultEvaluation EvaluateWorkflow(WorkflowExecutionResult result);

    ToolResultEvaluation EvaluateObservation(AgentPlanObservation observation);

    ToolResultEvaluation EvaluateTool(ToolExecutionResult result);
}

public sealed class RuleBasedToolResultEvaluator : IToolResultEvaluator
{
    public ToolResultEvaluation EvaluateWorkflow(WorkflowExecutionResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.ClarifyQuestion)
            || string.Equals(result.ErrorKind, "empty", StringComparison.OrdinalIgnoreCase)
            || string.Equals(result.Error, "empty_result", StringComparison.OrdinalIgnoreCase))
        {
            return new ToolResultEvaluation
            {
                Kind = ToolResultKind.Empty,
                Phase = "clarify",
                UserMessage = string.IsNullOrWhiteSpace(result.ClarifyQuestion)
                    ? "没有找到匹配的产品或库存记录，请确认型号/SKU 后重试。"
                    : result.ClarifyQuestion!
            };
        }

        if (result.Success)
        {
            return new ToolResultEvaluation
            {
                Kind = ToolResultKind.Success,
                Phase = "tool_ok",
                UserMessage = string.IsNullOrWhiteSpace(result.Summary)
                    ? "查询已完成。"
                    : result.Summary
            };
        }

        if (IsAuth(result.ErrorKind, result.HttpStatusCode, result.Error, result.Summary))
        {
            return new ToolResultEvaluation
            {
                Kind = ToolResultKind.AuthFailed,
                Phase = "auth_failed",
                UserMessage =
                    "下游接口鉴权失败（HTTP 401/403），未能查询到真实业务数据。"
                    + "请携带有效 AccessToken 或配置凭证后重试。本次不会返回任何库存/订单数字。"
            };
        }

        if (LooksLikeStub(result.Summary))
        {
            return new ToolResultEvaluation
            {
                Kind = ToolResultKind.Stub,
                Phase = "stub_blocked",
                UserMessage = "下游返回的是演示/回退数据，不能作为真实库存或订单答复。请修复 API 后重试。"
            };
        }

        return new ToolResultEvaluation
        {
            Kind = ToolResultKind.Error,
            Phase = "tool_error",
            UserMessage = !string.IsNullOrWhiteSpace(result.Summary)
                ? result.Summary!
                : "业务操作失败，未能取得真实结果，请稍后重试或联系管理员。"
                  + (string.IsNullOrWhiteSpace(result.Error) ? string.Empty : $"（{result.Error}）")
        };
    }

    public ToolResultEvaluation EvaluateObservation(AgentPlanObservation observation)
    {
        if (observation.Success)
        {
            return new ToolResultEvaluation
            {
                Kind = ToolResultKind.Success,
                Phase = "tool_ok",
                UserMessage = string.IsNullOrWhiteSpace(observation.Summary)
                    ? "查询已完成。"
                    : observation.Summary
            };
        }

        if (string.Equals(observation.ErrorKind, "empty", StringComparison.OrdinalIgnoreCase)
            || (observation.Summary?.Contains("未找到", StringComparison.Ordinal) ?? false))
        {
            return new ToolResultEvaluation
            {
                Kind = ToolResultKind.Empty,
                Phase = "clarify",
                UserMessage = string.IsNullOrWhiteSpace(observation.Summary)
                    ? "没有找到匹配结果，请确认型号/SKU。"
                    : observation.Summary
            };
        }

        if (IsAuth(observation.ErrorKind, observation.HttpStatusCode, observation.Error, observation.Summary))
        {
            return new ToolResultEvaluation
            {
                Kind = ToolResultKind.AuthFailed,
                Phase = "auth_failed",
                UserMessage =
                    "下游接口鉴权失败（HTTP 401/403），未能查询到真实业务数据。"
                    + "请检查凭证后重试。本次不会返回任何库存/订单数字。"
            };
        }

        return new ToolResultEvaluation
        {
            Kind = ToolResultKind.Error,
            Phase = "tool_error",
            UserMessage =
                "业务查询失败，未能取得真实数据。"
                + (string.IsNullOrWhiteSpace(observation.Error)
                    ? string.Empty
                    : $"（{observation.Error}）")
        };
    }

    public ToolResultEvaluation EvaluateTool(ToolExecutionResult result)
    {
        if (result.Success)
        {
            return new ToolResultEvaluation
            {
                Kind = ToolResultKind.Success,
                Phase = "tool_ok",
                UserMessage = string.IsNullOrWhiteSpace(result.Summary) ? "查询已完成。" : result.Summary
            };
        }

        if (IsAuth(null, result.HttpStatusCode, result.Error, result.Summary))
        {
            return new ToolResultEvaluation
            {
                Kind = ToolResultKind.AuthFailed,
                Phase = "auth_failed",
                UserMessage =
                    "下游接口鉴权失败（HTTP 401/403），未能查询到真实业务数据。"
                    + "请检查凭证后重试。本次不会返回任何库存/订单数字。"
            };
        }

        return new ToolResultEvaluation
        {
            Kind = ToolResultKind.Error,
            Phase = "tool_error",
            UserMessage = !string.IsNullOrWhiteSpace(result.Summary)
                ? result.Summary!
                : "业务操作失败，未能取得真实结果。"
                  + (string.IsNullOrWhiteSpace(result.Error) ? string.Empty : $"（{result.Error}）")
        };
    }

    private static bool IsAuth(string? errorKind, int? status, string? error, string? summary) =>
        string.Equals(errorKind, "auth", StringComparison.OrdinalIgnoreCase)
        || status is 401 or 403
        || (error?.Contains("HTTP 401", StringComparison.OrdinalIgnoreCase) ?? false)
        || (error?.Contains("HTTP 403", StringComparison.OrdinalIgnoreCase) ?? false)
        || (error?.Contains("鉴权", StringComparison.Ordinal) ?? false)
        || (summary?.Contains("鉴权失败", StringComparison.Ordinal) ?? false)
        || (summary?.Contains("HTTP 401", StringComparison.OrdinalIgnoreCase) ?? false);

    private static bool LooksLikeStub(string? summary) =>
        summary?.Contains("已回退 stub", StringComparison.OrdinalIgnoreCase) ?? false;
}
