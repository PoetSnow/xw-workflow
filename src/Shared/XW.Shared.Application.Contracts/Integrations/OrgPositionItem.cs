namespace XW.Shared.Application.Contracts.Integrations;

/// <summary>岗位只读项。</summary>
public sealed class OrgPositionItem
{
    /// <summary>岗位 Id。</summary>
    public long Id { get; set; }

    /// <summary>岗位编码。</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>岗位名称。</summary>
    public string Name { get; set; } = string.Empty;
}
