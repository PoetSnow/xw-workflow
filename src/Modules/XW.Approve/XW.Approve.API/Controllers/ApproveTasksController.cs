using Microsoft.AspNetCore.Http;
using XW.Approve.Application.Contracts.Dtos;
using XW.Approve.Application.Contracts.Interfaces;
using XW.Shared.WebApi.Routing;

namespace XW.Approve.API.Controllers;

/// <summary>审批待办：我的待办、通过、驳回、协作完成、知会已读、附件。</summary>
[Route($"{ApiSurfaces.ConsoleRoutePrefix}/approve/tasks")]
public sealed class ApproveTasksController(
    IApproveInstanceService service,
    IApproveTaskFileService files) : ConsoleApiController
{
    /// <summary>我的待办。</summary>
    [HttpGet("mine")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PageModelDto<ApproveTaskListItemDto>>> GetMyTasksPagedAsync(
        [FromQuery] ApproveSearchDto input)
        => await service.GetMyTasksPagedAsync(input);

    /// <summary>通过当前待办。</summary>
    [HttpPost("{id:long}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ApproveAsync([FromRoute] long id, [FromBody] ApproveHandleDto? input)
        => Result(await service.ApproveAsync(id, input ?? new ApproveHandleDto()));

    /// <summary>驳回当前待办，实例结束。</summary>
    [HttpPost("{id:long}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RejectAsync([FromRoute] long id, [FromBody] ApproveHandleDto? input)
        => Result(await service.RejectAsync(id, input ?? new ApproveHandleDto()));

    /// <summary>协作或任务完成。</summary>
    [HttpPost("{id:long}/complete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CompleteAsync([FromRoute] long id, [FromBody] ApproveCompleteDto? input)
        => Result(await service.CompleteAsync(id, input ?? new ApproveCompleteDto()));

    /// <summary>知会已读。</summary>
    [HttpPost("{id:long}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ReadAsync([FromRoute] long id)
        => Result(await service.ReadAsync(id));

    /// <summary>上传协作或任务附件。</summary>
    [HttpPost("{id:long}/files")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApproveTaskFileDto>> UploadAsync([FromRoute] long id, [FromForm] ApproveTaskFileUploadForm form)
    {
        if (form.File is null)
        {
            return BadRequest("请选择文件");
        }

        await using var stream = form.File.OpenReadStream();
        return Result(await files.UploadAsync(id, form.File.FileName, form.File.ContentType, form.File.Length, stream));
    }

    /// <summary>删除尚未提交的附件。</summary>
    [HttpDelete("{id:long}/files/{fileId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeleteFileAsync([FromRoute] long id, [FromRoute] long fileId)
        => Result(await files.DeleteAsync(id, fileId));

    /// <summary>下载附件。</summary>
    [HttpGet("files/{fileId:long}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAsync([FromRoute] long fileId)
    {
        var opened = await files.OpenAsync(fileId);
        if (opened is null)
        {
            return NotFound();
        }

        return File(opened.Stream, opened.ContentType, opened.FileName);
    }
}

/// <summary>附件上传表单。</summary>
public sealed class ApproveTaskFileUploadForm
{
    /// <summary>文件。</summary>
    public IFormFile? File { get; set; }
}
