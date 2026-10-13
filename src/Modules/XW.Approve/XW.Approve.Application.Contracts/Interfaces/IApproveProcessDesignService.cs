using XW.Approve.Application.Contracts.Dtos;
using XW.Infra.Repository.Interceptor;

namespace XW.Approve.Application.Contracts.Interfaces;

/// <summary>流程可视化设计：整图保存、发布、条件字段。</summary>
public interface IApproveProcessDesignService : IAppService
{
    /// <summary>流程版本列表。</summary>
    Task<IReadOnlyList<ApproveProcessListItemDto>> ListAsync();

    /// <summary>设计图。不存在返回 null。</summary>
    Task<ApproveProcessDesignDto?> GetAsync(long id);

    /// <summary>新建草稿，画布上只有开始到结束。</summary>
    [OperateLog(LogName = "新建审批流程")]
    [UnitOfWork]
    Task<ServiceResult<IdDto>> CreateAsync(ApproveProcessCreateDto input);

    /// <summary>覆盖草稿整张图。已发布版本须先生成新版本。</summary>
    [OperateLog(LogName = "保存审批流程")]
    [UnitOfWork]
    Task<ServiceResult> SaveAsync(long id, ApproveProcessSaveDto input);

    /// <summary>发布草稿，并写成该业务类型的默认流程。</summary>
    [OperateLog(LogName = "发布审批流程")]
    [UnitOfWork]
    Task<ServiceResult> PublishAsync(long id);

    /// <summary>从当前版本复制出下一版草稿。</summary>
    [OperateLog(LogName = "复制审批流程版本")]
    [UnitOfWork]
    Task<ServiceResult<IdDto>> NewVersionAsync(long id);

    /// <summary>删除草稿。</summary>
    [OperateLog(LogName = "删除审批流程")]
    [UnitOfWork]
    Task<ServiceResult> DeleteAsync(long id);

    /// <summary>业务条件字段。</summary>
    Task<IReadOnlyList<ApproveBusinessFieldDto>> ListFieldsAsync(string businessType);

    /// <summary>按业务类型替换条件字段。</summary>
    [OperateLog(LogName = "保存审批条件字段")]
    [UnitOfWork]
    Task<ServiceResult> SaveFieldsAsync(string businessType, IReadOnlyList<ApproveBusinessFieldDto> fields);
}
