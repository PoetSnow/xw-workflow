using Microsoft.AspNetCore.Mvc;
using XW.Agent.Application.Capability;
using XW.Agent.Application.Options;
using XW.Agent.Application.Tooling;

namespace XW.Agent.Application.Controllers;

/// <summary>Capability 注册中心 API（目录 / 检索）。</summary>
[ApiController]
[Route("api/agent/capabilities")]
public sealed class AgentCapabilitiesController(
    ICapabilityRegistry registry,
    ICapabilityRetriever retriever) : ControllerBase
{
    [HttpGet]
    public ActionResult<object> List([FromQuery] string? domain = null, [FromQuery] string? tag = null)
    {
        IReadOnlyList<AgentCapabilityDefinition> source = !string.IsNullOrWhiteSpace(domain)
            ? registry.ListByDomain(domain)
            : !string.IsNullOrWhiteSpace(tag)
                ? registry.ListByTag(tag)
                : registry.ListEnabled();

        var items = source.Select(MapSummary);
        return Ok(new
        {
            count = source.Count,
            domains = registry.ListDomains(),
            items
        });
    }

    /// <summary>动态 Tool 检索预览（Planner 实际也走同一 Retriever）。</summary>
    [HttpGet("search")]
    public async Task<ActionResult<object>> Search(
        [FromQuery] string q,
        [FromQuery] int topK = 8,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { message = "q is required" });
        }

        var result = await retriever.RetrieveAsync(new CapabilityRetrievalRequest
        {
            Query = q,
            Candidates = registry.ListEnabled(),
            TopK = topK <= 0 ? 8 : topK,
            ExpandRelated = true
        }, ct);

        return Ok(new
        {
            query = q,
            strategy = result.Strategy,
            candidateCount = result.CandidateCount,
            hits = result.Hits.Select(h => new
            {
                h.Capability.Code,
                h.Capability.Name,
                h.Capability.Domain,
                h.Score,
                h.Reason,
                h.Capability.WhenToUse
            })
        });
    }

    [HttpGet("{code}")]
    public ActionResult<object> Get(string code)
    {
        var c = registry.Get(code);
        if (c is null)
        {
            return NotFound(new { message = $"Capability '{code}' not found" });
        }

        return Ok(c);
    }

    private static object MapSummary(AgentCapabilityDefinition c) => new
    {
        c.Code,
        c.Name,
        c.Description,
        c.WhenToUse,
        c.Domain,
        c.Tags,
        c.Aliases,
        c.RelatedCodes,
        c.Risk,
        permission = c.ResolvePermissions(),
        c.Permission,
        c.IntentCode,
        c.ConfirmRequired,
        c.Version,
        parameters = c.Parameters.Select(p => new
        {
            p.Name,
            p.Description,
            p.Required,
            p.Type
        }),
        http = c.Http is null
            ? null
            : new
            {
                c.Http.Adapter,
                c.Http.AuthProfile,
                c.Http.BaseUrl,
                c.Http.Method,
                c.Http.Path,
                query = c.Http.Query,
                c.Http.TimeoutSeconds,
                c.Http.MaxRetries,
                c.Http.FallbackToStub,
                hasBodyTemplate = !string.IsNullOrWhiteSpace(c.Http.BodyTemplate)
            }
    };
}
