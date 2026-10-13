using XW.Shared.Pot.Enums;

namespace XW.Shared.Pot.Mqtt;

/// <summary>MQTT 信封。payload 随 type 变化，用泛型承载。</summary>
/// <typeparam name="TPayload">COMMAND / ACK 等具体载荷。</typeparam>
public sealed class PotMqttEnvelope<TPayload>
{
    /// <summary>协议版本，当前固定 pot.cmd.v1。</summary>
    public string Schema { get; set; } = PotMqttSchemas.CmdV1;

    /// <summary>消息 Id，雪花 long。</summary>
    public long MsgId { get; set; }

    /// <summary>可选的字符串形式 msgId，调试用。</summary>
    public string? MsgIdStr { get; set; }

    /// <summary>设备事件时间，Unix 秒。</summary>
    public long Ts { get; set; }

    /// <summary>设备 SN。</summary>
    public string Sn { get; set; } = string.Empty;

    /// <summary>信封类型：COMMAND / ACK / SHADOW / EVENT / CAPABILITY。</summary>
    public PotMessageType Type { get; set; }

    /// <summary>业务载荷。</summary>
    public TPayload? Payload { get; set; }
}
