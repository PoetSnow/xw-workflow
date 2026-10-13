using XW.Approve.Application.Contracts.Dtos;
using XW.Infra.Repository.Interceptor;

namespace XW.Approve.Application.Contracts.Interfaces;

/// <summary>协作和任务待办的附件。</summary>
public interface IApproveTaskFileService : IAppService
{
    /// <summary>当前办理人上传。环节须允许附件。</summary>
    [OperateLog(LogName = "上传办理附件")]
    [UnitOfWork]
    Task<ServiceResult<ApproveTaskFileDto>> UploadAsync(
        long taskId,
        string fileName,
        string? contentType,
        long size,
        Stream content);

    /// <summary>当前办理人在完成前删除自己上传的附件。</summary>
    [OperateLog(LogName = "删除办理附件")]
    [UnitOfWork]
    Task<ServiceResult> DeleteAsync(long taskId, long fileId);

    /// <summary>下载。不存在返回 null。</summary>
    Task<ApproveTaskFileContent?> OpenAsync(long fileId);
}
