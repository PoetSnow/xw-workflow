using XW.Shared.Pot.Enums;

namespace XW.Shared.Pot.Mqtt;

/// <summary>下行 COMMAND 载荷。一条指令只允许一个动作。</summary>
public sealed class PotCommandPayload
{
    /// <summary>指令 Id。见过的 commandId 只回放 ACK，不重做。</summary>
    public long CommandId { get; set; }

    /// <summary>烹饪任务 Id。</summary>
    public long TaskId { get; set; }

    /// <summary>配方步骤 Id。</summary>
    public long StepId { get; set; }

    /// <summary>任务世代。旧 epoch 包一律 REJECTED。</summary>
    public int Epoch { get; set; }

    /// <summary>步骤序号。设备只执行期望的下一步。</summary>
    public int StepNo { get; set; }

    /// <summary>同一步内的投递序号。</summary>
    public int Seq { get; set; }

    /// <summary>幂等键，通常 {taskId}:{stepId}:{seq}。</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>动作：DISPENSE / HEAT / KEEP_WARM / STOP / PING。</summary>
    public CommandAction Action { get; set; }

    /// <summary>仓位码，如 S3。HEAT / STOP 可为空。</summary>
    public string? SlotCode { get; set; }

    /// <summary>投料计量方式。非 DISPENSE 可为空。</summary>
    public DispenseMode? Mode { get; set; }

    /// <summary>投料数量。</summary>
    public PotAmount? Amount { get; set; }

    /// <summary>触发条件。DISPENSE 水温开火必须带 TEMP_GTE，禁止无 Trigger 立即投。</summary>
    public PotTrigger? Trigger { get; set; }

    /// <summary>过期时间，Unix 秒。过期回 REJECTED。</summary>
    public long? ExpireAt { get; set; }
}
