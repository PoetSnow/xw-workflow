using XW.Shared.Pot.Enums;

namespace XW.Shared.Pot.Mqtt;

/// <summary>指令触发条件。水温开火只认硬件本地探头，不认影子。</summary>
public sealed class PotTrigger
{
    /// <summary>触发类型。</summary>
    public TriggerType Type { get; set; }

    /// <summary>TEMP_GTE / TEMP_LTE 的摄氏度阈值。</summary>
    public decimal? TempCelsius { get; set; }

    /// <summary>DELAY 的秒数。</summary>
    public int? DelaySeconds { get; set; }
}
