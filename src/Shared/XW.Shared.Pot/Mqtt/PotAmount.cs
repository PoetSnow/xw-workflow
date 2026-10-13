using XW.Shared.Pot.Enums;

namespace XW.Shared.Pot.Mqtt;

/// <summary>投料数量。</summary>
public sealed class PotAmount
{
    /// <summary>数值。</summary>
    public decimal Value { get; set; }

    /// <summary>单位：G / PACK / PCS / SEC。</summary>
    public AmountUnit Unit { get; set; }
}
