using System.Text.Json;
using System.Text.Json.Serialization;

namespace XW.Shared.Pot.Json;

/// <summary>壶协议 JSON 约定：camelCase，枚举写大写字符串。</summary>
public static class PotJson
{
    /// <summary>序列化选项。MQTT 上下行共用，勿改成数字枚举。</summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    /// <summary>序列化为 JSON 文本。</summary>
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    /// <summary>从 JSON 文本反序列化。</summary>
    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
        // 枚举必须输出 WEIGHT / TEMP_GTE 这种文档字面量，不能是 0/1
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
