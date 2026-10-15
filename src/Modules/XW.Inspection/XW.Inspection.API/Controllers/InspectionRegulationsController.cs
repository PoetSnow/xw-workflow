using Microsoft.AspNetCore.Http;
using XW.Inspection.Application;
using XW.Inspection.Application.Contracts;
using XW.Shared.WebApi.Routing;

namespace XW.Inspection.API.Controllers;

/// <summary>法规目录：上传 PDF、确认发布、改适用行业和项目类型。</summary>
[Route($"{ApiSurfaces.ConsoleRoutePrefix}/inspection/regulations")]
public sealed class InspectionRegulationsController(IInspectionAppService service) : ConsoleApiController
{
    /// <summary>法规目录。</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RegulationDto>>> ListAsync(CancellationToken cancellationToken)
        => Ok(await service.ListRegulationsAsync(cancellationToken));

    /// <summary>上传 PDF。行业或项目类型为空时保持未发布。</summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(52_428_800)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RegulationDto>> UploadAsync(
        [FromForm] IFormFile? file,
        [FromForm] string? name,
        [FromForm] string? standardNo,
        [FromForm] string[]? industries,
        [FromForm] string[]? projectTypes,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "未收到 PDF。" });
        }

        await using var content = file.OpenReadStream();
        return await RunAsync(() => service.UploadAsync(new UploadRegulationRequest
        {
            Content = content,
            FileName = file.FileName,
            Name = name ?? string.Empty,
            StandardNo = standardNo ?? string.Empty,
            Industries = industries ?? [],
            ProjectTypes = projectTypes ?? []
        }, cancellationToken));
    }

    /// <summary>确认发布。行业和项目类型都要有值。</summary>
    [HttpPost("{id:long}/publish")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<RegulationDto>> PublishAsync([FromRoute] long id, CancellationToken cancellationToken)
        => RunAsync(() => service.PublishAsync(id, cancellationToken));

    /// <summary>废止。不重新解析 PDF。</summary>
    [HttpPost("{id:long}/withdraw")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<RegulationDto>> WithdrawAsync([FromRoute] long id, CancellationToken cancellationToken)
        => RunAsync(() => service.WithdrawAsync(id, cancellationToken));

    /// <summary>只改适用项目类型，不重新上传。</summary>
    [HttpPatch("{id:long}/project-types")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<RegulationDto>> ReplaceProjectTypesAsync(
        [FromRoute] long id,
        [FromBody] ReplaceTagsRequest request,
        CancellationToken cancellationToken)
        => RunAsync(() => service.ReplaceProjectTypesAsync(id, request.Values, cancellationToken));

    /// <summary>只改适用行业，不重新上传。</summary>
    [HttpPatch("{id:long}/industries")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<RegulationDto>> ReplaceIndustriesAsync(
        [FromRoute] long id,
        [FromBody] ReplaceTagsRequest request,
        CancellationToken cancellationToken)
        => RunAsync(() => service.ReplaceIndustriesAsync(id, request.Values, cancellationToken));

    private async Task<ActionResult<RegulationDto>> RunAsync(Func<Task<RegulationDto>> action)
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
