using System.Text.Json.Serialization;

namespace XW.Shared.Pot.Enums;

/// <summary>投料数量单位。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AmountUnit
{
    /// <summary>克。</summary>
    G,

    /// <summary>包。</summary>
    PACK,

    /// <summary>个。</summary>
    PCS,

    /// <summary>秒。</summary>
    SEC
}
