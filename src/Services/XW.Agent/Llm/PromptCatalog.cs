using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using XW.Agent.Application.Options;

namespace XW.Agent.Application.Llm;

/// <summary>Prompt 渲染：回复 / Planner / TaskPlan 模板，禁止散落硬编码。</summary>
public interface IPromptCatalog
{
    string RenderIntentSystem();

    string RenderIntentUser(IReadOnlyDictionary<string, string> vars);

    /// <summary>Action 车道回复 system。</summary>
    string RenderReplySystem();

    /// <summary>Action 车道回复 user。</summary>
    string RenderReplyUser(IReadOnlyDictionary<string, string> vars);

    /// <summary>Knowledge 车道回复 system。</summary>
    string RenderKnowledgeReplySystem();

    /// <summary>Knowledge 车道回复 user。</summary>
    string RenderKnowledgeReplyUser(IReadOnlyDictionary<string, string> vars);

    string RenderPlannerSystem();

    string RenderPlannerUser(IReadOnlyDictionary<string, string> vars);

    /// <summary>Action 入口 Intent+Entity 抽取 system。</summary>
    string RenderTaskPlanSystem();

    /// <summary>Action 入口 Intent+Entity 抽取 user。</summary>
    string RenderTaskPlanUser(IReadOnlyDictionary<string, string> vars);
}

/// <summary>从 <see cref="AgentOptions.Prompts"/> 读取模板；空则用内置 Default*。</summary>
public sealed class OptionsPromptCatalog(IOptionsMonitor<AgentOptions> options) : IPromptCatalog
{
    private static readonly Regex Placeholder = new(@"\{\{\s*(?<name>[a-zA-Z0-9_]+)\s*\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string RenderIntentSystem()
    {
        var p = GetPrompts();
        return string.IsNullOrWhiteSpace(p.IntentSystem)
            ? AgentPromptOptions.DefaultIntentSystem
            : p.IntentSystem;
    }

    public string RenderIntentUser(IReadOnlyDictionary<string, string> vars)
    {
        var p = GetPrompts();
        var tpl = string.IsNullOrWhiteSpace(p.IntentUserTemplate)
            ? AgentPromptOptions.DefaultIntentUser
            : p.IntentUserTemplate;
        return Render(tpl, vars);
    }

    public string RenderReplySystem()
    {
        var p = GetPrompts();
        return string.IsNullOrWhiteSpace(p.ReplySystem)
            ? AgentPromptOptions.DefaultReplySystem
            : p.ReplySystem;
    }

    public string RenderReplyUser(IReadOnlyDictionary<string, string> vars)
    {
        var p = GetPrompts();
        var tpl = string.IsNullOrWhiteSpace(p.ReplyUserTemplate)
            ? AgentPromptOptions.DefaultReplyUser
            : p.ReplyUserTemplate;
        return Render(tpl, vars);
    }

    public string RenderKnowledgeReplySystem()
    {
        var p = GetPrompts();
        return string.IsNullOrWhiteSpace(p.KnowledgeReplySystem)
            ? AgentPromptOptions.DefaultKnowledgeReplySystem
            : p.KnowledgeReplySystem;
    }

    public string RenderKnowledgeReplyUser(IReadOnlyDictionary<string, string> vars)
    {
        var p = GetPrompts();
        var tpl = string.IsNullOrWhiteSpace(p.KnowledgeReplyUserTemplate)
            ? AgentPromptOptions.DefaultKnowledgeReplyUser
            : p.KnowledgeReplyUserTemplate;
        return Render(tpl, vars);
    }

    public string RenderPlannerSystem()
    {
        var p = GetPrompts();
        return string.IsNullOrWhiteSpace(p.PlannerSystem)
            ? AgentPromptOptions.DefaultPlannerSystem
            : p.PlannerSystem;
    }

    public string RenderPlannerUser(IReadOnlyDictionary<string, string> vars)
    {
        var p = GetPrompts();
        var tpl = string.IsNullOrWhiteSpace(p.PlannerUserTemplate)
            ? AgentPromptOptions.DefaultPlannerUser
            : p.PlannerUserTemplate;
        return Render(tpl, vars);
    }

    public string RenderTaskPlanSystem()
    {
        var p = GetPrompts();
        return string.IsNullOrWhiteSpace(p.TaskPlanSystem)
            ? AgentPromptOptions.DefaultTaskPlanSystem
            : p.TaskPlanSystem;
    }

    public string RenderTaskPlanUser(IReadOnlyDictionary<string, string> vars)
    {
        var p = GetPrompts();
        var tpl = string.IsNullOrWhiteSpace(p.TaskPlanUserTemplate)
            ? AgentPromptOptions.DefaultTaskPlanUser
            : p.TaskPlanUserTemplate;
        return Render(tpl, vars);
    }

    private AgentPromptOptions GetPrompts() =>
        options.CurrentValue.Prompts ?? new AgentPromptOptions();

    private static string Render(string template, IReadOnlyDictionary<string, string> vars) =>
        Placeholder.Replace(template, m =>
        {
            var name = m.Groups["name"].Value;
            return vars.TryGetValue(name, out var v) ? v ?? string.Empty : string.Empty;
        });
}
