namespace XW.Approve.Application.Contracts.Dtos;

/// <summary>审批结束通知。Approved / Rejected / Cancelled。</summary>
public sealed class ApproveCompletedNotification
{
    public long InstanceId { get; init; }

    public string BusinessType { get; init; } = string.Empty;

    public string BusinessId { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public long SubmitUserId { get; init; }
}
