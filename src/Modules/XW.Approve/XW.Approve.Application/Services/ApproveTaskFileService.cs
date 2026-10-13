using XW.Approve.Application.Contracts.Dtos;
using XW.Approve.Application.Contracts.Interfaces;
using XW.Approve.Domain.Constants;
using XW.Approve.Domain.Entities;
using XW.Approve.Domain.Exceptions;
using XW.Approve.Domain.Ports;
using XW.Approve.Domain.Repositories;
using XW.Approve.Domain.Rules;

namespace XW.Approve.Application.Services;

/// <summary>协作和任务附件：校验办理人与清单，再落记录和文件。</summary>
public sealed class ApproveTaskFileService(
    IApproveInstanceRepository instances,
    IApproveProcessGraphRepository graphs,
    IOrgSnapshotReader orgSnapshotReader,
    IApproveTaskFileRepository files,
    IApproveTaskFileStore store,
    UserContext userContext) : AbstractAppService, IApproveTaskFileService
{
    private const long MaxBytes = 20 * 1024 * 1024;

    /// <inheritdoc />
    public async Task<ServiceResult<ApproveTaskFileDto>> UploadAsync(
        long taskId,
        string fileName,
        string? contentType,
        long size,
        Stream content)
    {
        try
        {
            var userId = RequireUserId();
            if (size <= 0 || size > MaxBytes)
            {
                return Problem(HttpStatusCode.BadRequest, "文件不能为空，且不能超过 20MB");
            }

            var safeName = CleanFileName(fileName);
            var (instance, task) = await RequirePendingCompleteTask(taskId, userId);
            var schema = await SchemaOf(instance, task);
            if (!FormSchemaRules.AllowsAttachment(schema))
            {
                return Problem(HttpStatusCode.BadRequest, "本环节不接收附件");
            }

            var id = IdGenerater.GetNextId();
            var key = await store.SaveAsync(task.Id, id, content);
            var file = new ApproveTaskFile
            {
                Id = id,
                TaskId = task.Id,
                InstanceId = instance.Id,
                FileName = safeName,
                ContentType = TrimContentType(contentType),
                FileSize = size,
                StorageKey = key,
                CreateBy = userId,
                CreateTime = DateTime.Now,
            };
            try
            {
                await files.AddAsync(file);
            }
            catch
            {
                store.Delete(key);
                throw;
            }

            return ToDto(file);
        }
        catch (ApprovalDomainException ex)
        {
            return Problem(HttpStatusCode.BadRequest, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteAsync(long taskId, long fileId)
    {
        try
        {
            var userId = RequireUserId();
            var (_, task) = await RequirePendingCompleteTask(taskId, userId);
            var file = await files.FindAsync(fileId) ?? throw new ApprovalDomainException("附件不存在");
            if (file.TaskId != task.Id)
            {
                throw new ApprovalDomainException("附件不属于当前待办");
            }

            store.Delete(file.StorageKey);
            await files.RemoveAsync(file);
            return ServiceResult();
        }
        catch (ApprovalDomainException ex)
        {
            return Problem(HttpStatusCode.BadRequest, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<ApproveTaskFileContent?> OpenAsync(long fileId)
    {
        if (userContext.Id <= 0)
        {
            return null;
        }

        var file = await files.FindAsync(fileId);
        if (file is null)
        {
            return null;
        }

        try
        {
            return new ApproveTaskFileContent
            {
                Stream = store.OpenRead(file.StorageKey),
                FileName = file.FileName,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            };
        }
        catch (ApprovalDomainException)
        {
            return null;
        }
    }

    private async Task<(Domain.Aggregates.ApproveInstance Instance, Domain.Aggregates.ApproveTask Task)> RequirePendingCompleteTask(
        long taskId,
        long userId)
    {
        var instance = await instances.GetByTaskIdForWriteAsync(taskId)
            ?? throw new ApprovalDomainException("任务不存在");
        var task = instance.Tasks.FirstOrDefault(t => t.Id == taskId)
            ?? throw new ApprovalDomainException("任务不存在");
        if (task.Status != ApproveTaskStatus.Pending || task.AssigneeUserId != userId)
        {
            throw new ApprovalDomainException(task.Status == ApproveTaskStatus.Pending ? "无权处理该任务" : "任务已处理");
        }

        if (!ApproveToken.Equals(task.TaskType, ApproveTaskType.Complete))
        {
            throw new ApprovalDomainException("当前环节不能上传附件");
        }

        if (instance.Status != ApproveInstanceStatus.Running || instance.CurrentNodeId != task.InstanceNodeId)
        {
            throw new ApprovalDomainException("任务不属于当前节点");
        }

        return (instance, task);
    }

    private async Task<string?> SchemaOf(Domain.Aggregates.ApproveInstance instance, Domain.Aggregates.ApproveTask task)
    {
        var graph = await graphs.GetByProcessDefIdAsync(instance.ProcessDefId)
            ?? throw new ApprovalDomainException("流程定义不存在");
        var org = await orgSnapshotReader.LoadAsync();
        var node = instance.Nodes.FirstOrDefault(n => n.Id == task.InstanceNodeId)
            ?? throw new ApprovalDomainException("实例节点不存在");
        return FormSchemaSelector.ForTask(graph, node.ProcessNodeId, task, instance.SubmitUserId, org);
    }

    private long RequireUserId()
    {
        if (userContext.Id <= 0)
        {
            throw new ApprovalDomainException("未登录");
        }

        return userContext.Id;
    }

    private static string CleanFileName(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            throw new ApprovalDomainException("文件名无效");
        }

        var chars = name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch).ToArray();
        name = new string(chars);
        return name.Length <= ApproveTaskFile.FileNameMaxLength
            ? name
            : name[..ApproveTaskFile.FileNameMaxLength];
    }

    private static string TrimContentType(string? contentType)
    {
        var value = (contentType ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return "application/octet-stream";
        }

        return value.Length <= ApproveTaskFile.ContentTypeMaxLength
            ? value
            : value[..ApproveTaskFile.ContentTypeMaxLength];
    }

    private static ApproveTaskFileDto ToDto(ApproveTaskFile file) => new()
    {
        Id = file.Id,
        TaskId = file.TaskId,
        FileName = file.FileName,
        ContentType = file.ContentType,
        FileSize = file.FileSize,
    };
}
