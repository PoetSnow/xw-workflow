namespace XW.Agent.Application.Options;

/// <summary>目录模式：legacy=Capability≈API；layered=Business Capability + Tool + Workflow。</summary>
public sealed class AgentCatalogOptions
{
    /// <summary>legacy | layered</summary>
    public string Mode { get; set; } = "legacy";

    public bool IsLayered =>
        string.Equals(Mode, "layered", StringComparison.OrdinalIgnoreCase);
}

/// <summary>业务能力入口：直绑 Tool 或走 Workflow。</summary>
public sealed class AgentCapabilityEntry
{
    /// <summary>tool | workflow</summary>
    public string Kind { get; set; } = "tool";

    /// <summary>Tool.Code 或 Workflow.Code</summary>
    public string Target { get; set; } = string.Empty;
}

/// <summary>声明式线性工作流定义（固定多步，非 BPM）。</summary>
public sealed class AgentWorkflowDefinition
{
    /// <summary>工作流编码，如 wf.inventory.query.by_keyword。</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>展示名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>是否启用。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>入口参数（通常与业务能力 Parameters 对齐）。</summary>
    public List<AgentCapabilityParameter> Input { get; set; } = [];

    /// <summary>有序步骤列表。</summary>
    public List<AgentWorkflowStep> Steps { get; set; } = [];
}

/// <summary>工作流中的单步：调某个 Tool，并可选赋值变量。</summary>
public sealed class AgentWorkflowStep
{
    /// <summary>步骤 Id（如 s1、s2）。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>技术层 Tool.Code。</summary>
    public string Tool { get; set; } = string.Empty;

    /// <summary>参数模板，如 keyword={{keyword}}；值来自 session/slots/vars。</summary>
    public Dictionary<string, string> Arguments { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 从工具结果写入工作流变量。Phase1 支持简单路径：
    /// productId={{result.data.items[0].id}} 或 productId={{firstId}}（由执行器约定）。
    /// </summary>
    public Dictionary<string, string> Assign { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// clarify = 追问；fail = 工作流失败；continue = 跳过赋值进下一步；
    /// ok = 空结果视为成功业务事实（如库存账 0 条 = 现存 0）。
    /// </summary>
    public string OnEmpty { get; set; } = "fail";

    /// <summary>
    /// 若指定变量已有非空值则跳过本步（如已解析到 productId 则不再搜物料）。
    /// </summary>
    public string? SkipIfVarPresent { get; set; }
}
