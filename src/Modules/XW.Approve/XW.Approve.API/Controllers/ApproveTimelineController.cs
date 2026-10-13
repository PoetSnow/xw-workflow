using Microsoft.AspNetCore.Http;
using XW.Approve.Application.Contracts.Dtos;
using XW.Approve.Application.Contracts.Interfaces;
using XW.Shared.WebApi.Routing;

namespace XW.Approve.API.Controllers;

/// <summary>单据竖排时间线。</summary>
[Route($"{ApiSurfaces.ConsoleRoutePrefix}/approve/timeline")]
public sealed class ApproveTimelineController(IApproveInstanceService service) : ConsoleApiController
{
    /// <summary>按业务单据取时间线。</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApproveTimelineBoardDto>> GetByBusinessAsync(
        [FromQuery] string businessType,
        [FromQuery] string businessId)
    {
        var dto = await service.GetTimelineByBusinessAsync(businessType, businessId);
        return dto is null ? NotFound() : dto;
    }

    /// <summary>按实例 Id 取该单据全部审批轮次。</summary>
    [HttpGet("{instanceId:long}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApproveTimelineBoardDto>> GetByInstanceAsync([FromRoute] long instanceId)
    {
        var dto = await service.GetTimelineAsync(instanceId);
        return dto is null ? NotFound() : dto;
    }
}
