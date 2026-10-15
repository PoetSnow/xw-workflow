using Microsoft.AspNetCore.Http;
using XW.Inspection.Application;
using XW.Inspection.Application.Contracts;
using XW.Shared.WebApi.Routing;

namespace XW.Inspection.API.Controllers;

/// <summary>检测项目草稿：按行业和项目类型匹配法规，列出检测项。</summary>
[Route($"{ApiSurfaces.ConsoleRoutePrefix}/inspection/projects")]
public sealed class InspectionProjectsController(IInspectionAppService service) : ConsoleApiController
{
    /// <summary>新建草稿并按目录匹配已发布资料。</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<ProjectDto>> CreateAsync([FromBody] CreateProjectRequest request, CancellationToken cancellationToken)
        => RunAsync(() => service.CreateProjectAsync(request, cancellationToken));

    /// <summary>打开草稿并重新匹配。已去掉的资料不会加回。</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<ProjectDto>> GetAsync([FromRoute] long id, CancellationToken cancellationToken)
        => RunAsync(() => service.GetProjectAsync(id, cancellationToken));

    /// <summary>从项目中去掉一套资料。</summary>
    [HttpPost("{id:long}/regulations/{regulationId:long}/remove")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<ProjectDto>> RemoveRegulationAsync(
        [FromRoute] long id,
        [FromRoute] long regulationId,
        CancellationToken cancellationToken)
        => RunAsync(() => service.RemoveRegulationAsync(id, regulationId, cancellationToken));

    /// <summary>从已发布目录补一套资料。</summary>
    [HttpPost("{id:long}/regulations/{regulationId:long}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<ProjectDto>> AddRegulationAsync(
        [FromRoute] long id,
        [FromRoute] long regulationId,
        CancellationToken cancellationToken)
        => RunAsync(() => service.AddRegulationAsync(id, regulationId, cancellationToken));

    /// <summary>只在留下的资料里列出检测项。不覆盖人工改过的行。</summary>
    [HttpPost("{id:long}/items/list")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<ProjectDto>> ListItemsAsync([FromRoute] long id, CancellationToken cancellationToken)
        => RunAsync(() => service.ListItemsAsync(id, cancellationToken));

    /// <summary>手工加一条检测项。</summary>
    [HttpPost("{id:long}/items")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ActionResult<ProjectDto>> AddItemAsync(
        [FromRoute] long id,
        [FromBody] UpsertItemRequest request,
        CancellationToken cancellationToken)
        => RunAsync(() => service.AddItemAsync(id, request, cancellationToken));

    /// <summary>修改检测项。改过的行再次分析时不覆盖。</summary>
    [HttpPut("{id:long}/items/{itemId:long}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<ProjectDto>> UpdateItemAsync(
        [FromRoute] long id,
        [FromRoute] long itemId,
        [FromBody] UpsertItemRequest request,
        CancellationToken cancellationToken)
        => RunAsync(() => service.UpdateItemAsync(id, itemId, request, cancellationToken));

    /// <summary>删除一条检测项。</summary>
    [HttpDelete("{id:long}/items/{itemId:long}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<ProjectDto>> RemoveItemAsync(
        [FromRoute] long id,
        [FromRoute] long itemId,
        CancellationToken cancellationToken)
        => RunAsync(() => service.RemoveItemAsync(id, itemId, cancellationToken));

    private async Task<ActionResult<ProjectDto>> RunAsync(Func<Task<ProjectDto>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (InspectionNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InspectionRuleException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
