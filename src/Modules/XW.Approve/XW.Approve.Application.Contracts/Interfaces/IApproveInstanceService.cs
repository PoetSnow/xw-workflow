using XW.Approve.Application.Contracts.Dtos;
using XW.Shared.Interception;

namespace XW.Approve.Application.Contracts.Interfaces;

/// <summary>审批实例应用服务：提交、处理、查询。流转规则在 Domain。</summary>
public interface IApproveInstanceService : IAppService
{
    /// <summary>发起审批。同一业务进行中只允许一条 Running。</summary>
    [OperateLog(LogName = "提交审批")]
    [UnitOfWork]
    Task<ServiceResult<IdDto>> SubmitAsync(ApproveSubmitDto input);

    /// <summary>通过当前待办。</summary>
    [OperateLog(LogName = "审批通过")]
    [UnitOfWork(ModuleScope = ModuleUnitOfWorkScope.SupplyChainWithApprove)]
    Task<ServiceResult> ApproveAsync(long taskId, ApproveHandleDto input);

    /// <summary>驳回当前待办，实例结束。</summary>
    [OperateLog(LogName = "审批驳回")]
    [UnitOfWork(ModuleScope = ModuleUnitOfWorkScope.SupplyChainWithApprove)]
    Task<ServiceResult> RejectAsync(long taskId, ApproveHandleDto input);

    /// <summary>协作或任务完成。</summary>
    [OperateLog(LogName = "办理完成")]
    [UnitOfWork(ModuleScope = ModuleUnitOfWorkScope.SupplyChainWithApprove)]
    Task<ServiceResult> CompleteAsync(long taskId, ApproveCompleteDto input);

    /// <summary>知会已读。</summary>
    [OperateLog(LogName = "知会已读")]
    [UnitOfWork(ModuleScope = ModuleUnitOfWorkScope.SupplyChainWithApprove)]
    Task<ServiceResult> ReadAsync(long taskId);

    /// <summary>发起人撤销进行中的实例。</summary>
    [OperateLog(LogName = "撤销审批")]
    [UnitOfWork(ModuleScope = ModuleUnitOfWorkScope.SupplyChainWithApprove)]
    Task<ServiceResult> CancelAsync(long instanceId, ApproveCancelDto input);

    /// <summary>实例详情（节点、任务、历史）。</summary>
    Task<ApproveInstanceDetailDto?> GetAsync(long instanceId);

    /// <summary>我的待办。</summary>
    Task<PageModelDto<ApproveTaskListItemDto>> GetMyTasksPagedAsync(ApproveSearchDto input);

    /// <summary>我发起的。</summary>
    Task<PageModelDto<ApproveInstanceListItemDto>> GetMySubmittedPagedAsync(ApproveSearchDto input);

    /// <summary>按业务单据取全部审批轮次。没有记录返回 null。</summary>
    [UnitOfWork]
    Task<ApproveTimelineBoardDto?> GetTimelineByBusinessAsync(string businessType, string businessId);

    /// <summary>按实例取该单据全部审批轮次。</summary>
    [UnitOfWork]
    Task<ApproveTimelineBoardDto?> GetTimelineAsync(long instanceId);
}
