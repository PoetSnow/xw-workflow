namespace XW.Approve.Application.Contracts;

/// <summary>业务类型令牌。与 Domain PascalCase 一致，业务模块只引用本层。</summary>
public static class ApproveBusinessTypes
{
    /// <summary>采购单。</summary>
    public const string Purchase = "Purchase";
}

/// <summary>实例状态。与 appr_instance.status 一致。</summary>
public static class ApproveInstanceStatuses
{
    public const string Running = "Running";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
    public const string Terminated = "Terminated";
}
