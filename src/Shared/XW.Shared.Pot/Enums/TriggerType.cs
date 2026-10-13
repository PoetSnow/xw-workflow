using System.Text.Json.Serialization;

namespace XW.Shared.Pot.Enums;

/// <summary>指令触发条件。水温开火只认硬件本地探头。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TriggerType
{
    /// <summary>本地水温 ≥ 阈值才动作。</summary>
    TEMP_GTE,

    /// <summary>本地水温 ≤ 阈值才动作。</summary>
    TEMP_LTE,

    /// <summary>延迟若干秒后动作。</summary>
    DELAY,

    /// <summary>收到即执行，无条件等待。</summary>
    IMMEDIATE
}
