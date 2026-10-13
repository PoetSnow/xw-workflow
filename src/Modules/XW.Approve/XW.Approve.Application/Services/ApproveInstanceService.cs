using XW.Approve.Application.Contracts;
using XW.Approve.Application.Contracts.Dtos;
using XW.Approve.Application.Contracts.Interfaces;
using XW.Approve.Application.Mappers;
using XW.Approve.Domain.Aggregates;
using XW.Approve.Domain.Constants;
using XW.Approve.Domain.Entities;
using XW.Approve.Domain.Exceptions;
using XW.Approve.Domain.Models;
using XW.Approve.Domain.Ports;
using XW.Approve.Domain.Queries;
using XW.Approve.Domain.Repositories;
using XW.Approve.Domain.Rules;
using Microsoft.Extensions.Logging;

namespace XW.Approve.Application.Services;

/// <summary>
/// 鉴权上下文、加载图与组织快照、调聚合、落库、查列表。不写流转 if/else。
/// </summary>
public sealed class ApproveInstanceService(
    IApproveInstanceRepository instanceRepository,
    IApproveProcessGraphRepository graphRepository,
    IApproveProcessBindingRepository bindingRepository,
    IApproveProcessDesignRepository designRepository,
    IEnumerable<IApproveDocumentFieldSource> documentSources,
    IOrgSnapshotReader orgSnapshotReader,
    IApproveActorDirectory actorDirectory,
    IApproveQuery approveQuery,
    IApproveTaskFileRepository taskFiles,
    UserContext userContext,
    IEnumerable<IApproveCompletedHandler> completedHandlers,
    ILogger<ApproveInstanceService> logger) : AbstractAppService, IApproveInstanceService
{
    /// <inheritdoc />
    public async Task<ServiceResult<IdDto>> SubmitAsync(ApproveSubmitDto input)
    {
        input.TrimStringFields();
        try
        {
            var userId = RequireUserId();
            var fallbackDeptId = await orgSnapshotReader.GetUserDeptIdAsync(userId) ?? 0;

            var businessType = input.BusinessType?.Trim() ?? string.Empty;
            var businessId = input.BusinessId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(businessType) || string.IsNullOrWhiteSpace(businessId))
            {
                throw new ApprovalDomainException("业务类型和业务主键不能为空");
            }

            var running = await instanceRepository.FindRunningAsync(businessType, businessId);
            if (running is not null)
            {
                throw new ApprovalDomainException("该业务已有进行中的审批");
            }

            var binding = await bindingRepository.FindForBusinessAsync(businessType, input.BusinessSubType);
            if (binding is null)
            {
                throw new ApprovalDomainException($"未配置业务 {businessType} 的审批流程");
            }

            var graph = await graphRepository.GetActiveByProcessDefIdAsync(binding.ProcessDefId)
                ?? throw new ApprovalDomainException("流程定义不可用或未发布");

            var org = await orgSnapshotReader.LoadAsync();
            var snapshot = await BindSnapshotAsync(businessType, businessId, fallbackDeptId);
            var deptId = ReadSnapshotDeptId(snapshot);
            if (deptId <= 0)
            {
                throw new ApprovalDomainException("单据缺少审批部门");
            }

            var title = string.IsNullOrWhiteSpace(input.Title) ? $"{businessType} {businessId}" : input.Title.Trim();

            var instance = ApproveInstance.Start(
                binding.ProcessDefId,
                businessType,
                businessId,
                title,
                userId,
                deptId,
                snapshot,
                graph,
                org);

            await instanceRepository.InsertAsync(instance);
            await NotifyIfCompletedAsync(instance);
            return new IdDto(instance.Id);
        }
        catch (ApprovalDomainException ex)
        {
            return Problem(HttpStatusCode.BadRequest, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ApproveAsync(long taskId, ApproveHandleDto input)
    {
        input ??= new ApproveHandleDto();
        input.TrimStringFields();
        try
        {
            var userId = RequireUserId();
            var instance = await instanceRepository.GetByTaskIdForWriteAsync(taskId)
                ?? throw new ApprovalDomainException("任务不存在");

            var graph = await graphRepository.GetByProcessDefIdAsync(instance.ProcessDefId)
                ?? throw new ApprovalDomainException("流程定义不存在");
            var org = await orgSnapshotReader.LoadAsync();

            instance.Approve(taskId, userId, input.Comment, graph, org);
            await instanceRepository.UpdateAsync(instance);
            await NotifyIfCompletedAsync(instance);
            return ServiceResult();
        }
        catch (ApprovalDomainException ex)
        {
            return Problem(HttpStatusCode.BadRequest, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> RejectAsync(long taskId, ApproveHandleDto input)
    {
        input ??= new ApproveHandleDto();
        input.TrimStringFields();
        try
        {
            var userId = RequireUserId();
            var instance = await instanceRepository.GetByTaskIdForWriteAsync(taskId)
                ?? throw new ApprovalDomainException("任务不存在");

            instance.Reject(taskId, userId, input.Comment);
            await instanceRepository.UpdateAsync(instance);
            await NotifyIfCompletedAsync(instance);
            return ServiceResult();
        }
        catch (ApprovalDomainException ex)
        {
            return Problem(HttpStatusCode.BadRequest, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CompleteAsync(long taskId, ApproveCompleteDto input)
    {
        input ??= new ApproveCompleteDto();
        try
        {
            var userId = RequireUserId();
            var instance = await instanceRepository.GetByTaskIdForWriteAsync(taskId)
                ?? throw new ApprovalDomainException("任务不存在");
            var graph = await graphRepository.GetByProcessDefIdAsync(instance.ProcessDefId)
                ?? throw new ApprovalDomainException("流程定义不存在");
            var org = await orgSnapshotReader.LoadAsync();
            var task = instance.Tasks.First(t => t.Id == taskId);
            var node = instance.Nodes.First(n => n.Id == task.InstanceNodeId);
            var schema = FormSchemaSelector.ForTask(graph, node.ProcessNodeId, task, instance.SubmitUserId, org);
            var count = await taskFiles.CountByTaskAsync(taskId);
            instance.Complete(taskId, userId, ReadPayload(input.Payload), schema, count);
            await instanceRepository.UpdateAsync(instance);
            await NotifyIfCompletedAsync(instance);
            return ServiceResult();
        }
        catch (ApprovalDomainException ex)
        {
            return Problem(HttpStatusCode.BadRequest, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ReadAsync(long taskId)
    {
        try
        {
            var userId = RequireUserId();
            var instance = await instanceRepository.GetByTaskIdForWriteAsync(taskId)
                ?? throw new ApprovalDomainException("任务不存在");
            instance.MarkRead(taskId, userId);
            await instanceRepository.UpdateAsync(instance);
            await NotifyIfCompletedAsync(instance);
            return ServiceResult();
        }
        catch (ApprovalDomainException ex)
        {
            return Problem(HttpStatusCode.BadRequest, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> CancelAsync(long instanceId, ApproveCancelDto input)
    {
        input ??= new ApproveCancelDto();
        input.TrimStringFields();
        try
        {
            var userId = RequireUserId();
            var instance = await instanceRepository.GetForWriteAsync(instanceId)
                ?? throw new ApprovalDomainException("审批实例不存在");

            instance.Cancel(userId, input.Comment);
            await instanceRepository.UpdateAsync(instance);
            await NotifyIfCompletedAsync(instance);
            return ServiceResult();
        }
        catch (ApprovalDomainException ex)
        {
            return Problem(HttpStatusCode.BadRequest, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<ApproveInstanceDetailDto?> GetAsync(long instanceId)
    {
        var instance = await instanceRepository.GetWithChildrenAsync(instanceId);
        return instance is null ? null : ApproveDtoMapper.ToDetail(instance);
    }

    /// <inheritdoc />
    public async Task<PageModelDto<ApproveTaskListItemDto>> GetMyTasksPagedAsync(ApproveSearchDto input)
    {
        input ??= new ApproveSearchDto();
        input.TrimStringFields();
        var userId = userContext.Id;
        if (userId <= 0)
        {
            return new PageModelDto<ApproveTaskListItemDto>(input);
        }

        var result = await approveQuery.GetMyPendingTasksAsync(
            userId,
            input.Keywords,
            input.SkipRows(),
            input.PageSize);

        if (result.Total == 0)
        {
            return new PageModelDto<ApproveTaskListItemDto>(input);
        }

        return new PageModelDto<ApproveTaskListItemDto>(
            input,
            result.Items.Select(ApproveDtoMapper.ToTaskListItem).ToList(),
            result.Total);
    }

    /// <inheritdoc />
    public async Task<PageModelDto<ApproveInstanceListItemDto>> GetMySubmittedPagedAsync(ApproveSearchDto input)
    {
        input ??= new ApproveSearchDto();
        input.TrimStringFields();
        var userId = userContext.Id;
        if (userId <= 0)
        {
            return new PageModelDto<ApproveInstanceListItemDto>(input);
        }

        var result = await approveQuery.GetMySubmittedAsync(
            userId,
            input.Keywords,
            input.Status,
            input.SkipRows(),
            input.PageSize);

        if (result.Total == 0)
        {
            return new PageModelDto<ApproveInstanceListItemDto>(input);
        }

        return new PageModelDto<ApproveInstanceListItemDto>(
            input,
            result.Items.Select(ApproveDtoMapper.ToListItem).ToList(),
            result.Total);
    }

    /// <inheritdoc />
    public async Task<ApproveTimelineBoardDto?> GetTimelineByBusinessAsync(string businessType, string businessId)
    {
        var type = businessType?.Trim() ?? string.Empty;
        var id = businessId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var instances = await instanceRepository.ListByBusinessAsync(type, id);
        return instances.Count == 0 ? null : await MapTimelineBoardAsync(instances);
    }

    /// <inheritdoc />
    public async Task<ApproveTimelineBoardDto?> GetTimelineAsync(long instanceId)
    {
        var instance = await instanceRepository.GetWithChildrenAsync(instanceId);
        if (instance is null)
        {
            return null;
        }

        var instances = await instanceRepository.ListByBusinessAsync(instance.BusinessType, instance.BusinessId);
        if (instances.Count == 0)
        {
            instances = [instance];
        }

        return await MapTimelineBoardAsync(instances, instance.Id);
    }

    private async Task<ApproveInstance> RepairPathIfNeededAsync(ApproveInstance instance)
    {
        if (instance.Status != ApproveInstanceStatus.Running)
        {
            return instance;
        }

        try
        {
            var tracked = await instanceRepository.GetForWriteAsync(instance.Id) ?? instance;
            var graph = await graphRepository.GetByProcessDefIdAsync(tracked.ProcessDefId);
            if (graph is null)
            {
                return tracked;
            }

            var org = await orgSnapshotReader.LoadAsync();
            var snapshot = await BindSnapshotAsync(tracked.BusinessType, tracked.BusinessId, tracked.SubmitDeptId);
            if (tracked.SyncSnapshotAndPendingPath(snapshot, graph, org))
            {
                await instanceRepository.UpdateAsync(tracked);
            }

            return tracked;
        }
        catch (ApprovalDomainException ex)
        {
            logger.LogWarning(ex, "补全审批路径失败 InstanceId={InstanceId}", instance.Id);
            return instance;
        }
    }

    private async Task<ApproveTimelineBoardDto> MapTimelineBoardAsync(
        IReadOnlyList<ApproveInstance> instances,
        long? currentInstanceId = null)
    {
        var repaired = new List<ApproveInstance>(instances.Count);
        foreach (var instance in instances)
        {
            repaired.Add(instance.Status == ApproveInstanceStatus.Running
                ? await RepairPathIfNeededAsync(instance)
                : instance);
        }

        var userIds = repaired
            .SelectMany(x => x.Tasks.Select(t => t.AssigneeUserId).Where(id => id.HasValue).Select(id => id!.Value))
            .Concat(repaired.Select(x => x.SubmitUserId))
            .Distinct()
            .ToList();
        var deptIds = repaired
            .SelectMany(x => x.Tasks.Select(t => t.DeptId).Where(id => id.HasValue).Select(id => id!.Value))
            .Concat(repaired.Select(x => x.SubmitDeptId))
            .Distinct()
            .ToList();
        var positionIds = repaired
            .SelectMany(x => x.Tasks.Select(t => t.PositionId).Where(id => id.HasValue).Select(id => id!.Value))
            .Distinct()
            .ToList();
        var userNames = await actorDirectory.GetUserNamesAsync(userIds);
        var deptNames = await actorDirectory.GetDeptNamesAsync(deptIds);
        var positionNames = await actorDirectory.GetPositionNamesAsync(positionIds);
        var files = await taskFiles.ListByInstancesAsync(repaired.Select(x => x.Id).ToList());
        var filesByTask = files
            .GroupBy(x => x.TaskId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(file => new ApproveTaskFileDto
                {
                    Id = file.Id,
                    TaskId = file.TaskId,
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    FileSize = file.FileSize,
                }).ToList());
        var schemas = await ResolveSchemasAsync(repaired);
        var currentId = currentInstanceId
                        ?? repaired.LastOrDefault(x => x.Status == ApproveInstanceStatus.Running)?.Id
                        ?? repaired.LastOrDefault()?.Id;

        var rounds = repaired
            .Select((instance, index) =>
            {
                var dto = ApproveDtoMapper.ToTimeline(
                    instance,
                    userContext.Id,
                    userNames,
                    deptNames,
                    positionNames,
                    schemas,
                    filesByTask);
                dto.Round = index + 1;
                dto.IsCurrent = instance.Id == currentId;
                return dto;
            })
            .ToList();

        return new ApproveTimelineBoardDto { Rounds = rounds };
    }

    private async Task<string> BindSnapshotAsync(string businessType, string businessId, long submitDeptId)
    {
        var fields = await designRepository.ListFieldsAsync(businessType);
        var columnKeys = fields
            .Where(IsDocumentColumn)
            .Select(x => x.FieldKey?.Trim() ?? string.Empty)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        IReadOnlyDictionary<string, object?> columns = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (columnKeys.Count > 0)
        {
            var source = documentSources.FirstOrDefault(x =>
                string.Equals(x.BusinessType, businessType, StringComparison.OrdinalIgnoreCase));
            if (source is null)
            {
                throw new ApprovalDomainException($"业务 {businessType} 未提供单据字段读取");
            }

            columns = await source.ReadColumnsAsync(businessId, columnKeys);
        }

        return FormSnapshotBinder.Bind(columns, fields, submitDeptId);
    }

    private static bool IsDocumentColumn(ApproveBusinessField field)
    {
        if (!field.Enabled)
        {
            return false;
        }

        if (ApproveToken.Equals(field.ValueSource, ApproveValueSource.System)
            && !string.Equals(field.FieldKey, "DeptId", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static long ReadSnapshotDeptId(string snapshot)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(snapshot) ? "{}" : snapshot);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!string.Equals(prop.Name, "DeptId", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Number && prop.Value.TryGetInt64(out var number))
                {
                    return number;
                }

                if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.String
                    && long.TryParse(prop.Value.GetString(), out var parsed))
                {
                    return parsed;
                }
            }
        }
        catch (System.Text.Json.JsonException)
        {
        }

        return 0;
    }

    private long RequireUserId()
    {
        if (userContext.Id <= 0)
        {
            throw new ApprovalDomainException("未登录");
        }

        return userContext.Id;
    }

    private static string ReadPayload(System.Text.Json.JsonElement payload) =>
        payload.ValueKind is System.Text.Json.JsonValueKind.Undefined or System.Text.Json.JsonValueKind.Null
            ? "{}"
            : payload.GetRawText();

    private async Task<Dictionary<long, string?>> ResolveSchemasAsync(IReadOnlyList<ApproveInstance> instances)
    {
        var schemas = new Dictionary<long, string?>();
        AssigneeResolveContext? org;
        try
        {
            org = await orgSnapshotReader.LoadAsync();
        }
        catch (ApprovalDomainException)
        {
            return schemas;
        }

        var graphs = new Dictionary<long, ProcessRuntimeGraph?>();
        foreach (var instance in instances)
        {
            if (!graphs.TryGetValue(instance.ProcessDefId, out var graph))
            {
                graph = await graphRepository.GetByProcessDefIdAsync(instance.ProcessDefId);
                graphs[instance.ProcessDefId] = graph;
            }

            if (graph is null)
            {
                continue;
            }

            foreach (var task in instance.Tasks)
            {
                var node = instance.Nodes.FirstOrDefault(n => n.Id == task.InstanceNodeId);
                if (node is null)
                {
                    continue;
                }

                schemas[task.Id] = FormSchemaSelector.ForTask(graph, node.ProcessNodeId, task, instance.SubmitUserId, org);
            }
        }

        return schemas;
    }

    private async Task NotifyIfCompletedAsync(ApproveInstance instance)
    {
        if (instance.Status == ApproveInstanceStatus.Running)
        {
            return;
        }

        var notification = new ApproveCompletedNotification
        {
            InstanceId = instance.Id,
            BusinessType = instance.BusinessType,
            BusinessId = instance.BusinessId,
            Status = instance.Status,
            SubmitUserId = instance.SubmitUserId,
        };

        foreach (var handler in completedHandlers.Where(h =>
                     string.Equals(h.BusinessType, instance.BusinessType, StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                await handler.HandleAsync(notification);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "审批结束回调失败 InstanceId={InstanceId} BusinessType={BusinessType} BusinessId={BusinessId} Status={Status}",
                    instance.Id,
                    instance.BusinessType,
                    instance.BusinessId,
                    instance.Status);
                throw;
            }
        }
    }
}
