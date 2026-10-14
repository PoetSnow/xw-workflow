using Microsoft.AspNetCore.Mvc;
using XW.Agent.Contracts.Jobs;

namespace XW.Agent.Host.Controllers;

/// <summary>进程内作业入口。请求和响应都是作业信封。</summary>
[ApiController]
[Route("api/agent/jobs")]
public sealed class AgentJobController(IJobDispatcher dispatcher) : ControllerBase
{
    /// <summary>同步执行一封作业。未知 kind 返回 failed。</summary>
    [HttpPost]
    public async Task<ActionResult<JobResult>> Dispatch(
        [FromBody] JobRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { message = "job envelope is required" });
        }

        var result = await dispatcher.DispatchAsync(request, cancellationToken);
        return Ok(result);
    }
}
