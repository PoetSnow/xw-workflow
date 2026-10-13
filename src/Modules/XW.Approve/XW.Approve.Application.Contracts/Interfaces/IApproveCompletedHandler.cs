using XW.Approve.Application.Contracts.Dtos;

namespace XW.Approve.Application.Contracts.Interfaces;

/// <summary>
/// 业务模块实现本接口以接收审批结束。Host 会注入全部实现。
/// 业务模块只依赖 Application.Contracts，不引用 Approve.Domain。
/// </summary>
public interface IApproveCompletedHandler
{
    /// <summary>处理的业务类型，如 Purchase。与提交时 BusinessType 一致。</summary>
    string BusinessType { get; }

    /// <summary>实例变为 Approved / Rejected / Cancelled 时调用。</summary>
    Task HandleAsync(ApproveCompletedNotification notification, CancellationToken cancellationToken = default);
}
