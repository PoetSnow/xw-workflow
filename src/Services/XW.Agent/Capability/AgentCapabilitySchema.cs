using System.Text.Json;
using XW.Agent.Application.Options;

namespace XW.Agent.Application.Capability;

/// <summary>
/// 由技能参数生成输入 JSON Schema。技能文件只写 Parameters，不手写第二份 Schema。
/// </summary>
public static class AgentCapabilitySchema
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null
    };

    /// <summary>生成 draft 风格的 object schema。必填项来自 Parameter.Required。</summary>
    public static JsonElement BuildInputSchema(AgentCapabilityDefinition capability)
    {
        var properties = new Dictionary<string, object>(StringComparer.Ordinal);
        var required = new List<string>();
        foreach (var parameter in capability.Parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.Name))
            {
                continue;
            }

            properties[parameter.Name] = new Dictionary<string, object>
            {
                ["type"] = MapType(parameter.Type),
                ["description"] = parameter.Description ?? string.Empty
            };
            if (parameter.Required)
            {
                required.Add(parameter.Name);
            }
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false
        };

        return JsonSerializer.SerializeToElement(schema, JsonOptions);
    }

    /// <summary>技能参数类型只映射到 JSON Schema 的 string / number / boolean / integer。</summary>
    private static string MapType(string? type)
    {
        return type?.Trim().ToLowerInvariant() switch
        {
            "number" or "float" or "double" or "decimal" => "number",
            "integer" or "int" or "long" => "integer",
            "bool" or "boolean" => "boolean",
            _ => "string"
        };
    }
}
