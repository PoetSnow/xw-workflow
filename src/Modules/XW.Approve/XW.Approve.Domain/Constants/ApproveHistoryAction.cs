namespace XW.Approve.Domain.Constants;

/// <summary>审批历史操作。对应 appr_history.action。</summary>
public static class ApproveHistoryAction
{
    /// <summary>提交：发起人创建实例。</summary>
    public const string Submit = "Submit";

    /// <summary>同意：审批人通过当前任务。</summary>
    public const string Approve = "Approve";

    /// <summary>驳回：审批人拒绝，实例结束为 Rejected。</summary>
    public const string Reject = "Reject";

    /// <summary>转交：任务转给他人。</summary>
    public const string Transfer = "Transfer";

    /// <summary>委托或加签。</summary>
    public const string Delegate = "Delegate";

    /// <summary>撤销：发起人撤回，实例结束为 Cancelled。</summary>
    public const string Cancel = "Cancel";

    /// <summary>跳过：按规则自动跳过其余待办。</summary>
    public const string Skip = "Skip";

    /// <summary>完成：协作或任务交齐本环节。</summary>
    public const string Complete = "Complete";

    /// <summary>已读：知会确认。</summary>
    public const string Read = "Read";
}
