using Microsoft.Extensions.Options;
using XW.Agent.Application.Options;
using XW.Agent.Application.Tooling;

namespace XW.Agent.Application.Capability;

/// <summary>Capability 注册中心：全量索引；Planner 不直接吃全量。</summary>
public interface ICapabilityRegistry
{
    /// <summary>列出启用能力（layered 时为业务能力视图）。</summary>
    IReadOnlyList<AgentCapabilityDefinition> ListEnabled();

    /// <summary>按 Code 获取（业务优先，再技术层）。</summary>
    AgentCapabilityDefinition? Get(string code);

    /// <summary>按 IntentCode 查找。</summary>
    AgentCapabilityDefinition? FindByIntent(string intentCode);

    /// <summary>按 Domain 过滤。</summary>
    IReadOnlyList<AgentCapabilityDefinition> ListByDomain(string domain);

    /// <summary>按 Tag 过滤。</summary>
    IReadOnlyList<AgentCapabilityDefinition> ListByTag(string tag);

    /// <summary>列出出现过的 Domain。</summary>
    IReadOnlyList<string> ListDomains();
}

/// <summary>
/// 配置驱动注册中心。
/// legacy：Capabilities（API 级）给 Planner。
/// layered：BusinessCapabilities 给 Planner；技术 Tools 仍在 Capabilities。
/// </summary>
public sealed class OptionsCapabilityRegistry(IOptionsMonitor<AgentOptions> options) : ICapabilityRegistry, ICapabilityCatalog
{
    public IReadOnlyList<AgentCapabilityDefinition> ListEnabled()
    {
        var opt = options.CurrentValue;
        if (opt.Catalog.IsLayered && opt.BusinessCapabilities.Count > 0)
        {
            return opt.BusinessCapabilities.Where(c => c.Enabled).ToList();
        }

        return opt.Capabilities.Where(c => c.Enabled).ToList();
    }

    public AgentCapabilityDefinition? Get(string code)
    {
        var opt = options.CurrentValue;
        if (opt.Catalog.IsLayered && opt.BusinessCapabilities.Count > 0)
        {
            var biz = opt.BusinessCapabilities.FirstOrDefault(c =>
                c.Enabled && string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));
            if (biz is not null)
            {
                return biz;
            }
        }

        return opt.Capabilities.FirstOrDefault(c =>
            c.Enabled && string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    public AgentCapabilityDefinition? FindByCode(string code) => Get(code);

    /// <summary>技术层 Tool（HTTP/Stub 绑定），供 Workflow 步骤与直绑执行。</summary>
    public AgentCapabilityDefinition? GetTechnicalTool(string code) =>
        options.CurrentValue.Capabilities.FirstOrDefault(c =>
            c.Enabled && string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));

    public AgentCapabilityDefinition? FindByIntent(string intentCode)
    {
        var caps = ListEnabled();
        return caps.FirstOrDefault(c =>
                   string.Equals(c.IntentCode, intentCode, StringComparison.OrdinalIgnoreCase))
               ?? caps.FirstOrDefault(c =>
                   string.Equals(c.Code, intentCode, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<AgentCapabilityDefinition> ListByDomain(string domain) =>
        ListEnabled()
            .Where(c => string.Equals(c.Domain, domain, StringComparison.OrdinalIgnoreCase))
            .ToList();

    public IReadOnlyList<AgentCapabilityDefinition> ListByTag(string tag) =>
        ListEnabled()
            .Where(c => c.Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)))
            .ToList();

    public IReadOnlyList<string> ListDomains() =>
        ListEnabled()
            .Select(c => c.Domain)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
