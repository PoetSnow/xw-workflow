namespace XW.Shared.Application.Contracts.Integrations;

/// <summary>部门只读项。</summary>
public sealed class OrgDeptItem
{
    /// <summary>部门 Id。</summary>
    public long Id { get; set; }

    /// <summary>上级部门 Id。</summary>
    public long ParentId { get; set; }

    /// <summary>部门名称。</summary>
    public string Name { get; set; } = string.Empty;
}
