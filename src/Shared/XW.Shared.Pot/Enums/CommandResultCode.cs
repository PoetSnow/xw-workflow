using System.Text.Json.Serialization;

namespace XW.Shared.Pot.Enums;

/// <summary>指令 ACK 结果。终态为 SUCCESS / FAILED / MISSING / EXCEPTION / REJECTED。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CommandResultCode
{
    /// <summary>设备已收到，可幂等回放。</summary>
    RECEIVED,

    /// <summary>正在执行。</summary>
    RUNNING,

    /// <summary>成功终态。</summary>
    SUCCESS,

    /// <summary>失败终态。</summary>
    FAILED,

    /// <summary>缺料终态。</summary>
    MISSING,

    /// <summary>异常终态。</summary>
    EXCEPTION,

    /// <summary>拒绝终态（乱序、过期、旧 epoch 等）。</summary>
    REJECTED
}
