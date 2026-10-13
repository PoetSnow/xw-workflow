using XW.Approve.Domain.Constants;
using XW.Infra.Repository;

namespace XW.Approve.Domain.Entities;

/// <summary>流程定义。按版本冻结，实例绑定 Id。P0 只读配置，不是运行时聚合根。</summary>
public class ApproveProcessDef : EfFullAuditEntity
{
    /// <summary>流程编码最大长度。</summary>
    public static readonly int ProcessKeyMaxLength = 64;

    /// <summary>流程名称最大长度。</summary>
    public static readonly int ProcessNameMaxLength = 128;

    /// <summary>业务类型最大长度。</summary>
    public static readonly int BusinessTypeMaxLength = 32;

    /// <summary>状态最大长度。</summary>
    public static readonly int StatusMaxLength = 16;

    /// <summary>流程编码，同编码可有多个版本。</summary>
    public string ProcessKey { get; set; } = string.Empty;

    /// <summary>流程名称。</summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>业务类型。</summary>
    public string BusinessType { get; set; } = string.Empty;

    /// <summary>版本号，从 1 起。</summary>
    public int Version { get; set; } = 1;

    /// <summary>状态，见 ApproveProcessStatus。</summary>
    public string Status { get; set; } = ApproveProcessStatus.Draft;
}
