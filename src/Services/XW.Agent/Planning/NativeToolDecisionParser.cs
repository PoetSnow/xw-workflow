using System.Text.Json;
using Microsoft.Extensions.AI;
using XW.Agent.Application.Options;

namespace XW.Agent.Application.Planning;

/// <summary>
/// 把原生 tool call 收成与 JSON 规划器相同的 <see cref="AgentPlanDecision"/>。
/// 编排器、权限闸门、确认闸门只看决策，不看协议。
/// </summary>
public static class NativeToolDecisionParser
{
    /// <summary>取第一条 function call。没有则返回 null，调用方可以再解析文本 JSON。</summary>
    public static AgentPlanDecision? FromResponse(
        ChatResponse response,
        IReadOnlyList<AgentCapabilityDefinition> tools)
    {
        foreach (var message in response.Messages)
        {
            foreach (var content in message.Contents)
            {
                if (content is FunctionCallContent call && !string.IsNullOrWhiteSpace(call.Name))
                {
                    return ToDecision(call.Name, ToArguments(call.Arguments), tools);
                }
            }
        }

        return null;
    }

    /// <summary>工具名必须在本轮候选里，否则收成 clarify，避免执行未登记能力。</summary>
    public static AgentPlanDecision ToDecision(
        string toolName,
        IReadOnlyDictionary<string, string> arguments,
        IReadOnlyList<AgentCapabilityDefinition> tools)
    {
        var known = tools.FirstOrDefault(x =>
            string.Equals(x.Code, toolName, StringComparison.OrdinalIgnoreCase));
        if (known is null)
        {
            return new AgentPlanDecision
            {
                Action = "clarify",
                Question = "我需要调用的接口不在可用目录中。请换一种业务描述，或联系管理员补充 Capability。",
                Reason = "unknown_tool"
            };
        }

        return new AgentPlanDecision
        {
            Action = "call_tool",
            Tool = known.Code,
            Arguments = new Dictionary<string, string>(arguments, StringComparer.OrdinalIgnoreCase),
            Reason = "native_tools"
        };
    }

    private static Dictionary<string, string> ToArguments(IDictionary<string, object?>? arguments)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (arguments is null)
        {
            return result;
        }

        foreach (var (key, value) in arguments)
        {
            var text = value switch
            {
                null => null,
                string s => s,
                JsonElement el when el.ValueKind == JsonValueKind.String => el.GetString(),
                JsonElement el => el.ToString(),
                _ => value.ToString()
            };
            if (!string.IsNullOrWhiteSpace(text))
            {
                result[key] = text!;
            }
        }

        return result;
    }
}
