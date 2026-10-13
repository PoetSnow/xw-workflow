using Microsoft.AspNetCore.Http;
using XW.Approve.Application.Contracts.Dtos;
using XW.Approve.Application.Contracts.Interfaces;
using XW.Shared.WebApi.Routing;

namespace XW.Approve.API.Controllers;

/// <summary>流程设计器：整图保存、发布、条件字段。</summary>
[Route($"{ApiSurfaces.ConsoleRoutePrefix}/approve/processes")]
public sealed class ApproveProcessesController(IApproveProcessDesignService service) : ConsoleApiController
{
    /// <summary>流程版本列表。</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ApproveProcessListItemDto>>> ListAsync()
        => Ok(await service.ListAsync());

    /// <summary>设计图。</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApproveProcessDesignDto>> GetAsync([FromRoute] long id)
    {
        var dto = await service.GetAsync(id);
        return dto is null ? NotFound() : dto;
    }

    /// <summary>新建草稿。</summary>
    [HttpPost]
    [ProducesResponseType(typeof(IdDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IdDto>> CreateAsync([FromBody] ApproveProcessCreateDto input)
        => CreatedResult(await service.CreateAsync(input));

    /// <summary>保存草稿整张图。</summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SaveAsync([FromRoute] long id, [FromBody] ApproveProcessSaveDto input)
        => Result(await service.SaveAsync(id, input));

    /// <summary>发布草稿，并绑定为该业务的默认流程。</summary>
    [HttpPost("{id:long}/publish")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> PublishAsync([FromRoute] long id)
        => Result(await service.PublishAsync(id));

    /// <summary>复制为下一版草稿。</summary>
    [HttpPost("{id:long}/versions")]
    [ProducesResponseType(typeof(IdDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<IdDto>> NewVersionAsync([FromRoute] long id)
        => CreatedResult(await service.NewVersionAsync(id));

    /// <summary>删除草稿。</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeleteAsync([FromRoute] long id)
        => Result(await service.DeleteAsync(id));

    /// <summary>业务条件字段。</summary>
    [HttpGet("fields")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ApproveBusinessFieldDto>>> ListFieldsAsync([FromQuery] string businessType)
        => Ok(await service.ListFieldsAsync(businessType));

    /// <summary>替换业务条件字段。</summary>
    [HttpPut("fields")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SaveFieldsAsync([FromQuery] string businessType, [FromBody] List<ApproveBusinessFieldDto> fields)
        => Result(await service.SaveFieldsAsync(businessType, fields));
}
