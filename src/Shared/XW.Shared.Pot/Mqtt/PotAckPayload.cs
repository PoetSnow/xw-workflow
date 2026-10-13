using XW.Shared.Pot.Enums;

namespace XW.Shared.Pot.Mqtt;

/// <summary>上行 ACK 载荷。RECEIVED → RUNNING → 唯一终态。</summary>
public sealed class PotAckPayload
{
    /// <summary>对应下行指令 Id。</summary>
    public long CommandId { get; set; }

    /// <summary>任务 Id。</summary>
    public long? TaskId { get; set; }

    /// <summary>任务世代。</summary>
    public int? Epoch { get; set; }

    /// <summary>步骤序号。</summary>
    public int? StepNo { get; set; }

    /// <summary>ACK 结果码。</summary>
    public CommandResultCode Result { get; set; }

    /// <summary>设备上报的实际水温，仅日志，不参与开火判定。</summary>
    public decimal? WaterTemp { get; set; }

    /// <summary>可选说明，如拒绝原因。</summary>
    public string? Message { get; set; }
}
