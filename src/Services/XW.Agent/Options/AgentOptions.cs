namespace XW.Agent.Application.Options;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    public string Channel { get; set; } = "web";
    public bool EnableStubTools { get; set; } = true;

    /// <summary>XW.Host 默认下游（Integration Adapter=host）。</summary>
    public AgentHostApiOptions HostApi { get; set; } = new();

    /// <summary>下游鉴权配置档。默认空；未配置时 PostConfigure 填入通用 Auth 类型模板。</summary>
    public List<AgentAuthProfileOptions> AuthProfiles { get; set; } = [];

    /// <summary>能力级权限策略（不是第二套用户中台）。</summary>
    public AgentPolicyOptions Policy { get; set; } = new();

    /// <summary>
    /// 技术层 Tool 目录（HTTP/Stub）。layered 模式下 Planner 不直接看这里；
    /// 执行与 Workflow 步骤通过 Code 查找。
    /// </summary>
    public List<AgentCapabilityDefinition> Capabilities { get; set; } = [];

    /// <summary>
    /// 业务能力目录（给 Planner）。仅 Catalog.Mode=layered 时作为检索/规划视图；
    /// Entry 指向 tool 或 workflow。
    /// </summary>
    public List<AgentCapabilityDefinition> BusinessCapabilities { get; set; } = [];

    /// <summary>声明式工作流（固定多步）。</summary>
    public List<AgentWorkflowDefinition> Workflows { get; set; } = [];

    /// <summary>Skill 文件目录（按业务域分文件夹，一接口一文件）。优先于 Capabilities 数组。</summary>
    public AgentSkillsOptions Skills { get; set; } = new();

    public AgentCatalogOptions Catalog { get; set; } = new();

    public AgentLlmOptions Llm { get; set; } = new();

    /// <summary>LLM Prompt 目录（意图分类 / 规划 / 对话回复约束）。</summary>
    public AgentPromptOptions Prompts { get; set; } = new();

    /// <summary>LLM 自主编排（推荐开启）：按 Capability 接口文档多步规划，而非 C# 写死流水线。</summary>
    public AgentPlannerOptions Planner { get; set; } = new();

    /// <summary>单轮对话的服务端截止时间。与知识库上传分开，避免一个全局超时切断大文件。</summary>
    public AgentRuntimeOptions Runtime { get; set; } = new();

    /// <summary>RAG 知识库（生产默认 vector；lexical 仅本地无库演示）。</summary>
    public AgentKnowledgeOptions Knowledge { get; set; } = new();

    public List<AgentIntentDefinition> Intents { get; set; } = [];
}

/// <summary>知识库 / RAG 配置（生产仅向量；关闭则不检索，不回退词库）。</summary>
public sealed class AgentKnowledgeOptions
{
    /// <summary>
    /// 总开关。false = Chat/检索完全不走知识库（不调 Embedding、不查库、不走词法）。
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// 检索实现：仅支持 vector（pgvector）。lexical 已废弃，配置了也会当作关闭。
    /// </summary>
    public string Provider { get; set; } = "vector";

    /// <summary>每轮对话自动检索并注入 Planner/Reply（需 Enabled 且 Embedding 可用）。</summary>
    public bool AutoRetrieve { get; set; } = true;

    public int TopK { get; set; } = 4;

    /// <summary>相对 Host ContentRoot 的种子目录（仅历史演示文件，向量模式不依赖）。</summary>
    public string SeedRelativePath { get; set; } = "knowledge/seed";

    /// <summary>上传原件相对 ContentRoot 目录。</summary>
    public string UploadRelativePath { get; set; } = "knowledge/uploads";

    /// <summary>单文件上传上限（字节）。</summary>
    public long MaxUploadBytes { get; set; } = 20 * 1024 * 1024;

    /// <summary>
    /// 上传接口的服务端截止时间（秒）。0 表示不另加截止，只跟随调用方取消。
    /// 默认 280，短于常见的 300 秒客户端超时，便于先返回明确失败。
    /// </summary>
    public int UploadDeadlineSeconds { get; set; } = 280;

    /// <summary>已废弃（词法演示用）。</summary>
    public double MinScore { get; set; } = 0.08;

    /// <summary>向量检索最低相似度（1 - cosine distance），0~1。</summary>
    public double MinSimilarity { get; set; } = 0.25;

    /// <summary>切块目标字符数。</summary>
    public int ChunkSize { get; set; } = 700;

    /// <summary>切块重叠字符数。</summary>
    public int ChunkOverlap { get; set; } = 80;

    /// <summary>PostgreSQL 连接串（需已安装 vector 扩展）。</summary>
    public string ConnectionString { get; set; } = string.Empty;

    public AgentEmbeddingOptions Embedding { get; set; } = new();
}

/// <summary>OpenAI 兼容 Embeddings HTTP。向量 RAG 的独立开关。</summary>
public sealed class AgentEmbeddingOptions
{
    /// <summary>
    /// Embedding 开关。false = 不调用 /v1/embeddings，知识检索直接跳过（不回退词库）。
    /// 总开关 Knowledge.Enabled 也必须为 true 才会检索。
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>根地址，如 http://192.168.1.150:8099</summary>
    public string Endpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Embedding 模型名；入库与查询必须一致。</summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>向量维数，须与模型输出及表结构一致。</summary>
    public int Dimensions { get; set; } = 1024;

    public string EmbeddingsPath { get; set; } = "/v1/embeddings";

    public int TimeoutSeconds { get; set; } = 60;
}

/// <summary>磁盘 Skill 目录：按业务域分子目录，一接口一 JSON，监视变更热加载。</summary>
public sealed class AgentSkillsOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>相对 ContentRoot 或绝对路径。默认 skills/</summary>
    public string RootPath { get; set; } = "skills";

    public bool ReloadOnChange { get; set; } = true;
}

/// <summary>对话运行时限额。集成调用的超时仍以各技能 Http.TimeoutSeconds 为准。</summary>
public sealed class AgentRuntimeOptions
{
    /// <summary>
    /// 一轮聊天的服务端截止时间（秒），从进入 Chat 动作起算。0 表示不另加截止。
    /// 默认 110，短于常见的 120 秒客户端超时。单次 LLM 调用仍使用更短的 HttpClient 超时。
    /// </summary>
    public int ChatDeadlineSeconds { get; set; } = 110;
}

/// <summary>Planner：LLM 读检索后的 Capability 子集，决定查什么、顺序、追问/确认。</summary>
public sealed class AgentPlannerOptions
{
    /// <summary>true=走 Planner 循环；false=回退旧 Intent→单 Tool。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>单轮对话最多调用工具次数（防死循环）。</summary>
    public int MaxSteps { get; set; } = 6;

    /// <summary>动态 Tool 检索（注册中心 → TopK → Planner）。</summary>
    public AgentCapabilityRetrievalOptions Retrieval { get; set; } = new();
}

/// <summary>Capability 注册中心之上的动态检索闸门。</summary>
public sealed class AgentCapabilityRetrievalOptions
{
    /// <summary>true=只把 TopK 能力塞进 Planner；false=全量（仅联调）。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>单次规划最多暴露的工具数。</summary>
    public int TopK { get; set; } = 8;

    /// <summary>命中后是否扩展 RelatedCodes（保证编排链不断）。</summary>
    public bool ExpandRelated { get; set; } = true;

    /// <summary>词法分最低阈值；0 表示只要有分即可。</summary>
    public double MinScore { get; set; }
}

/// <summary>XW.Host 接入（Agent 唯一业务下游）。</summary>
public sealed class AgentHostApiOptions
{
    /// <summary>XW.Host 根地址，如 http://localhost:5000</summary>
    public string BaseUrl { get; set; } = "http://localhost:5295";

    /// <summary>调用 Host 时可选的服务间凭证（Bearer）；可空。</summary>
    public string? AccessToken { get; set; }

    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// LLM 接入：多 Profile（本地/云端），运行时用 ActiveProfile 切换。
/// 兼容旧扁平字段 BaseUrl/ApiKey/Model（PostConfigure 会迁入 Profiles）。
/// </summary>
public sealed class AgentLlmOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>当前激活的 Profile Code。</summary>
    public string ActiveProfile { get; set; } = "local";

    public double ConfidenceThreshold { get; set; } = 0.6;
    public bool FallbackToRules { get; set; } = true;

    /// <summary>运行时覆盖是否落盘（重启后仍生效）。</summary>
    public bool PersistRuntimeOverrides { get; set; } = true;

    /// <summary>
    /// LLM Profile 列表。必须由配置提供（appsettings / 环境变量 / 运行时 API），
    /// 禁止在代码里写死 Endpoint/Model，避免覆盖真实环境。
    /// </summary>
    public List<AgentLlmProfileOptions> Profiles { get; set; } = [];

    // —— 旧扁平字段（兼容）；有值且 Profiles 空时自动迁入 ——
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? Model { get; set; }
    public float? Temperature { get; set; }
}

public sealed class AgentLlmProfileOptions
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>ollama | openai | vllm | lmstudio | custom</summary>
    public string ProviderCode { get; set; } = "openai";

    /// <summary>OpenAI 兼容根地址，如 http://192.168.1.150:8099</summary>
    public string Endpoint { get; set; } = string.Empty;

    public string ModelName { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public float Temperature { get; set; } = 0.1f;

    /// <summary>是否传 response_format=json_object；多数本地模型不支持，默认 false。</summary>
    public bool UseJsonResponseFormat { get; set; }

    /// <summary>
    /// 规划协议：json_decision（模型回一个 JSON 决策）或 native_tools（OpenAI tools 数组）。
    /// 跟当前选中的 Profile 走，不按模型名分支。编排器和确认闸门不因该字段分支。
    /// 配置里没写时按 json_decision，避免旧配置被悄悄改掉。
    /// </summary>
    public string ToolProtocol { get; set; } = AgentToolProtocols.JsonDecision;

    /// <summary>相对 Endpoint 的路径，默认 /v1/chat/completions。</summary>
    public string ChatCompletionsPath { get; set; } = "/v1/chat/completions";
}

/// <summary>Profile 上的工具协议。默认 JSON 决策，原生 tool call 必须显式打开。</summary>
public static class AgentToolProtocols
{
    public const string JsonDecision = "json_decision";
    public const string NativeTools = "native_tools";

    /// <summary>只有明确写成 native_tools 才走原生协议。</summary>
    public static bool IsNative(string? protocol) =>
        string.Equals(protocol?.Trim(), NativeTools, StringComparison.OrdinalIgnoreCase);

    /// <summary>空值回落到 json_decision，避免旧配置被当成原生协议。</summary>
    public static string Normalize(string? protocol) =>
        IsNative(protocol) ? NativeTools : JsonDecision;
}

public sealed class AgentIntentDefinition
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ToolName { get; set; }
    public string Permission { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = [];
    public List<AgentSlotDefinition> Slots { get; set; } = [];

    public static List<AgentIntentDefinition> DefaultCatalog() =>
    [
        new()
        {
            Code = "chitchat",
            Description = "闲聊或与进销存履约无关的通用问答，不需要调用业务工具",
            ToolName = null,
            Permission = string.Empty,
            Permissions = [],
            Slots = []
        }
    ];
}

public sealed class AgentSlotDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Required { get; set; }
}
