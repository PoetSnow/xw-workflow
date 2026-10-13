using System.Text.Json.Serialization;

namespace XW.Shared.Pot.Enums;

/// <summary>单条指令动作。一条指令只允许一个动作。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CommandAction
{
    /// <summary>投料。</summary>
    DISPENSE,

    /// <summary>加热到目标温度。</summary>
    HEAT,

    /// <summary>保温。</summary>
    KEEP_WARM,

    /// <summary>停止当前动作，可打断同 epoch。</summary>
    STOP,

    /// <summary>探活。</summary>
    PING
}
