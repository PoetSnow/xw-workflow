using System.Text.Json.Serialization;

namespace XW.Shared.Pot.Enums;

/// <summary>仓位物理类型。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SlotType
{
    /// <summary>散料仓。</summary>
    BULK,

    /// <summary>整包仓。</summary>
    PACKAGE,

    /// <summary>料盒仓。</summary>
    CARTRIDGE
}
