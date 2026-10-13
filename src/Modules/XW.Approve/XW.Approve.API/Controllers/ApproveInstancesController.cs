using Microsoft.AspNetCore.Http;
using XW.Approve.Application.Contracts.Dtos;
using XW.Approve.Application.Contracts.Interfaces;
using XW.Shared.WebApi.Routing;

namespace XW.Approve.API.Controllers;

/// <summary>审批实例：提交、详情、我发起的、撤销。</summary>
[Route($"{ApiSurfaces.ConsoleRoutePrefix}/approve/instances")]
public sealed class ApproveInstancesController(IApproveInstanceService service) : ConsoleApiController
{
    /// <summary>我发起的审批。</summary>
    [HttpGet("mine")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PageModelDto<ApproveInstanceListItemDto>>> GetMySubmittedPagedAsync(
        [FromQuery] ApproveSearchDto input)
        => await service.GetMySubmittedPagedAsync(input);

    /// <summary>审批实例详情（节点、任务、历史）。</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApproveInstanceDetailDto>> GetAsync([FromRoute] long id)
    {
        var dto = await service.GetAsync(id);
        return dto is null ? NotFound() : dto;
    }

    /// <summary>发起审批。同一业务进行中只允许一条。</summary>
    [HttpPost]
    [ProducesResponseType(typeof(IdDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IdDto>> SubmitAsync([FromBody] ApproveSubmitDto input)
        => CreatedResult(await service.SubmitAsync(input));

    /// <summary>发起人撤销进行中的审批。</summary>
    [HttpPost("{id:long}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CancelAsync([FromRoute] long id, [FromBody] ApproveCancelDto? input)
        => Result(await service.CancelAsync(id, input ?? new ApproveCancelDto()));
}
