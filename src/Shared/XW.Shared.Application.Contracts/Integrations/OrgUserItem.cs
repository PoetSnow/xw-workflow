namespace XW.Shared.Application.Contracts.Integrations;

/// <summary>用户只读项。</summary>
public sealed class OrgUserItem
{
    /// <summary>用户 Id。</summary>
    public long Id { get; set; }

    /// <summary>姓名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>账号。</summary>
    public string Account { get; set; } = string.Empty;

    /// <summary>主部门 Id。没有任职时，发起人部门回退到这个字段。</summary>
    public long DeptId { get; set; }
}
