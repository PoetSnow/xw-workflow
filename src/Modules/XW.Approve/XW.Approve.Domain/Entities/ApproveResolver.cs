using XW.Shared.Domain.Entities;

namespace XW.Approve.Domain.Entities;

/// <summary>审批人解析规则。类型见 ApproveResolverType。</summary>
public class ApproveResolver : FullAuditEntity
{
    /// <summary>解析类型最大长度。</summary>
    public static readonly int ResolverTypeMaxLength = 32;

    /// <summary>解析值最大长度。</summary>
    public static readonly int ResolverValueMaxLength = 64;

    /// <summary>所属流程节点 Id。</summary>
    public long ProcessNodeId { get; set; }

    /// <summary>解析类型，见 ApproveResolverType。</summary>
    public string ResolverType { get; set; } = string.Empty;

    /// <summary>岗位 Code 或用户 Id。</summary>
    public string ResolverValue { get; set; } = string.Empty;

    /// <summary>扩展 JSON。PositionInFixedDept 为 { "DeptId": 部门Id }。</summary>
    public string? ResolverExt { get; set; }

    /// <summary>覆盖节点默认清单。空则用节点 formschema。</summary>
    public string? FormSchema { get; set; }

    /// <summary>同一节点内顺序。</summary>
    public int Ordinal { get; set; }
}
