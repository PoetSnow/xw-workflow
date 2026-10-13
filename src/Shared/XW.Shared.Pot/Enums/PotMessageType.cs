using System.Text.Json.Serialization;

namespace XW.Shared.Pot.Enums;

/// <summary>MQTT 信封 type 字段。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PotMessageType
{
    /// <summary>下行指令。</summary>
    COMMAND,

    /// <summary>上行确认。</summary>
    ACK,

    /// <summary>设备影子快照。</summary>
    SHADOW,

    /// <summary>设备事件。</summary>
    EVENT,

    /// <summary>设备能力上报。</summary>
    CAPABILITY
}
