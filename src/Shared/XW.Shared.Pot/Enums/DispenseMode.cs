using System.Text.Json.Serialization;

namespace XW.Shared.Pot.Enums;

/// <summary>投料计量方式。JSON 必须是大写字符串，不能是数字。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DispenseMode
{
    /// <summary>按目标克数，硬件本地称重。</summary>
    WEIGHT,

    /// <summary>整包投放。</summary>
    PACKAGE,

    /// <summary>按个数。</summary>
    COUNT,

    /// <summary>按时间（秒）。</summary>
    TIME
}
