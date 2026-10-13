namespace XW.Agent.Application.Options;

/// <summary>
/// Capability 标准契约：语义描述 + 权限 + 参数 + HTTP 绑定。
/// Planner 只消费检索子集；全量目录进注册中心，禁止塞进每次 tools_json。
/// </summary>
public sealed class AgentCapabilityDefinition
{
    /// <summary>稳定唯一码，如 products.search</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>接口语义说明（给人或 Planner 读）：做什么、输入输出、边界。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>何时应调用（检索与规划共用）。</summary>
    public string WhenToUse { get; set; } = string.Empty;

    /// <summary>业务域，如 inventory / order / alert。</summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// 调用面。空 = 不区分。console = 运营后台，portal = 小程序会员自己的数据。
    /// 后台查看用菜单权限码，小程序写操作由业务接口按当前用户收口。
    /// </summary>
    public string Surface { get; set; } = string.Empty;

    /// <summary>检索标签。</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>同义词 / 别名（用户说法）。</summary>
    public List<string> Aliases { get; set; } = [];

    /// <summary>常一起用的能力码；检索命中后可扩展进候选集。</summary>
    public List<string> RelatedCodes { get; set; } = [];

    /// <summary>read | write | admin（也兼容配置 low/medium/high → 映射到 read/write/admin）。</summary>
    public string Risk { get; set; } = "read";

    /// <summary>
    /// 能力所需权限码列表（Sprint 3）。全部满足才可执行（AND）。
    /// 配置可只写本字段，或继续写单值 <see cref="Permission"/>。
    /// </summary>
    public List<string> Permissions { get; set; } = [];

    /// <summary>兼容旧单权限字段；Normalize 后与 Permissions[0] 对齐。</summary>
    public string Permission { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;
    public bool ConfirmRequired { get; set; }
    public string? IntentCode { get; set; }
    public string Version { get; set; } = "1";

    public List<AgentCapabilityParameter> Parameters { get; set; } = [];

    /// <summary>
    /// 下游响应里规划器可以引用的字段名。可选。旧技能不写则行为不变。
    /// 只给字段名和一句说明，不把原始 HTTP body 交给模型。
    /// </summary>
    public List<AgentCapabilityOutputHint> OutputHints { get; set; } = [];

    public AgentCapabilityHttpBinding? Http { get; set; }

    /// <summary>
    /// layered：业务入口。null 表示 legacy 自身即 Tool（Code 直接执行）。
    /// </summary>
    public AgentCapabilityEntry? Entry { get; set; }

    /// <summary>解析最终权限列表（Permissions 优先，否则回落 Permission）。</summary>
    public IReadOnlyList<string> ResolvePermissions()
    {
        if (Permissions.Count > 0)
        {
            return Permissions
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return string.IsNullOrWhiteSpace(Permission)
            ? Array.Empty<string>()
            : [Permission.Trim()];
    }

    /// <summary>Normalize：合并 Permission / Permissions，并规范化 Risk。</summary>
    public void NormalizeContract()
    {
        Risk = NormalizeRisk(Risk);

        if (Permissions.Count == 0 && !string.IsNullOrWhiteSpace(Permission))
        {
            Permissions = [Permission.Trim()];
        }
        else if (Permissions.Count > 0)
        {
            Permissions = Permissions
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (string.IsNullOrWhiteSpace(Permission))
            {
                Permission = Permissions[0];
            }
        }
    }

    private static string NormalizeRisk(string? risk)
    {
        if (string.IsNullOrWhiteSpace(risk))
        {
            return "read";
        }

        return risk.Trim().ToLowerInvariant() switch
        {
            "low" or "read" or "query" => "read",
            "medium" or "write" or "mutate" => "write",
            "high" or "admin" or "critical" => "admin",
            _ => risk.Trim().ToLowerInvariant()
        };
    }

    public static List<AgentCapabilityDefinition> DefaultCatalog() =>
    [
        new()
        {
            Code = "products.search",
            Name = "检索产品",
            Domain = "inventory",
            Tags = ["产品", "SKU", "品名", "检索"],
            Aliases = ["查产品", "找SKU", "产品搜索"],
            WhenToUse = "用户提到 SKU/品名/产品编码，需要解析为 productId，或查库存前缺少产品 Id。",
            RelatedCodes = ["inventory.balances.query", "materials.search"],
            Description =
                "按关键词（SKU/品名/编码）分页检索产品主档。返回列表含 id、code、name。" +
                "用户只说 SKU 或品名、要查库存时，应先调用本接口拿到 productId，再调 inventory.balances.query。" +
                "不要要求用户提供数据库 Id。",
            Permission = "agent.inventory.read",
            Permissions = ["agent.inventory.read"],
            IntentCode = "products.search",
            Risk = "read",
            Parameters =
            [
                new()
                {
                    Name = "keywords",
                    Description = "SKU、产品编码或品名，例如 SKU1001、阿莫西林",
                    Required = true,
                    Type = "string"
                }
            ],
            Http = new AgentCapabilityHttpBinding
            {
                Adapter = "host",
                AuthProfile = "forward-caller",
                Method = "GET",
                Path = "/api/console/product/Products",
                Query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["pageIndex"] = "1",
                    ["pageSize"] = "10",
                    ["keywords"] = "{{keywords}}"
                },
                TimeoutSeconds = 20,
                MaxRetries = 1,
                FallbackToStub = true
            }
        },
        new()
        {
            Code = "materials.search",
            Name = "检索物料",
            Domain = "inventory",
            Tags = ["物料", "编码", "检索"],
            Aliases = ["查物料", "物料搜索"],
            WhenToUse = "用户明确说物料，或产品检索无结果时，用物料主档解析 materialId。",
            RelatedCodes = ["inventory.balances.query", "products.search"],
            Description =
                "按关键词分页检索物料主档。返回列表含 id、code、name。" +
                "产品检索无结果或用户明确说物料时调用；拿到 materialId 后再查库存。",
            Permission = "agent.inventory.read",
            Permissions = ["agent.inventory.read"],
            IntentCode = "materials.search",
            Risk = "read",
            Parameters =
            [
                new()
                {
                    Name = "keywords",
                    Description = "物料编码或名称",
                    Required = true,
                    Type = "string"
                }
            ],
            Http = new AgentCapabilityHttpBinding
            {
                Adapter = "host",
                AuthProfile = "forward-caller",
                Method = "GET",
                Path = "/api/console/master-data/Materials",
                Query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["pageIndex"] = "1",
                    ["pageSize"] = "10",
                    ["keywords"] = "{{keywords}}"
                },
                TimeoutSeconds = 20,
                MaxRetries = 1,
                FallbackToStub = true
            }
        },
        new()
        {
            Code = "inventory.balances.query",
            Name = "查询库存账",
            Domain = "inventory",
            Tags = ["库存", "现存", "可售", "仓库"],
            Aliases = ["查库存", "库存多少", "还有多少货"],
            WhenToUse = "已有 productId 或 materialId，需要查询现存/预留数量。",
            RelatedCodes = ["products.search", "materials.search"],
            Description =
                "按 productId 或 materialId 查询数量库存账（现存/预留）。" +
                "必须先有有效 Id（来自 products.search / materials.search 结果，或用户明确给出的数字 Id）。" +
                "禁止编造 Id。productId 与 materialId 至少提供一个。",
            Permission = "agent.inventory.read",
            Permissions = ["agent.inventory.read"],
            IntentCode = "inventory.balances.query",
            Risk = "read",
            OutputHints =
            [
                new() { Name = "qtyOnHand", Description = "现存数量" }
            ],
            Parameters =
            [
                new()
                {
                    Name = "productId",
                    Description = "产品主键（来自 products.search 的 id）",
                    Required = false,
                    Type = "string"
                },
                new()
                {
                    Name = "materialId",
                    Description = "物料主键（来自 materials.search 的 id）",
                    Required = false,
                    Type = "string"
                },
                new()
                {
                    Name = "warehouseId",
                    Description = "仓库 Id（可选）",
                    Required = false,
                    Type = "string"
                }
            ],
            Http = new AgentCapabilityHttpBinding
            {
                Adapter = "host",
                AuthProfile = "forward-caller",
                Method = "GET",
                Path = "/api/console/supply-chain/InventoryBalances",
                Query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["pageIndex"] = "1",
                    ["pageSize"] = "20",
                    ["productId"] = "{{productId}}",
                    ["materialId"] = "{{materialId}}",
                    ["warehouseId"] = "{{warehouseId}}"
                },
                TimeoutSeconds = 20,
                MaxRetries = 1,
                FallbackToStub = false
            }
        },
        new()
        {
            Code = "order.fulfillment.query",
            Name = "查询订单履约",
            Domain = "order",
            Tags = ["订单", "履约", "物流", "发货"],
            Aliases = ["查订单", "物流到哪了", "发货状态"],
            WhenToUse = "用户提供或询问订单号的发货/物流状态。",
            RelatedCodes = [],
            Description = "按订单号查询发货与物流状态（待绑定真实 Host API 时可走 stub）",
            Permission = "agent.order.read",
            Permissions = ["agent.order.read"],
            IntentCode = "query_order_fulfillment",
            Risk = "read",
            Parameters =
            [
                new()
                {
                    Name = "orderNo",
                    Description = "订单号，如 SO202601010001",
                    Required = true,
                    Type = "string"
                }
            ]
        },
        new()
        {
            Code = "alert.low_stock.list",
            Name = "低库存预警",
            Domain = "alert",
            Tags = ["预警", "低库存", "安全库存"],
            Aliases = ["缺货预警", "库存告警", "哪些快断货"],
            WhenToUse = "用户要看低于安全库存或库存风险列表，不指定单一 SKU。",
            RelatedCodes = ["inventory.balances.query"],
            Description = "列出低于安全库存的商品（待绑定真实 Host API 时可走 stub）",
            Permission = "agent.alert.read",
            Permissions = ["agent.alert.read"],
            IntentCode = "list_low_stock_alerts",
            Risk = "read",
            Parameters = []
        },
        new()
        {
            Code = "knowledge.search",
            Name = "知识库检索",
            Domain = "knowledge",
            Tags = ["知识", "政策", "售后", "营销", "FAQ", "竞品"],
            Aliases = ["查知识库", "售后政策", "退货规则", "优惠券规则"],
            WhenToUse = "从企业知识库检索与用户问题相关的文档片段（政策、流程说明、FAQ）。不查询库存数字、不创建单据。",
            RelatedCodes = [],
            Description = "从企业知识库检索文档片段（售后、营销、库存FAQ、竞品要点）。返回带出处的摘要。",
            Permission = "agent.knowledge.read",
            Permissions = ["agent.knowledge.read"],
            IntentCode = "knowledge.search",
            Risk = "read",
            Parameters =
            [
                new()
                {
                    Name = "query",
                    Description = "检索问句，如「7天无理由退货」",
                    Required = true,
                    Type = "string"
                }
            ]
        }
    ];
}

public sealed class AgentCapabilityParameter
{
    /// <summary>参数名（写入 Planner 文档与 HTTP 模板占位符）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>参数语义说明。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>是否必填。</summary>
    public bool Required { get; set; }

    /// <summary>类型提示（string / number 等，供文档用）。</summary>
    public string Type { get; set; } = "string";
}

/// <summary>工具输出字段提示。规划器只看到名字和说明，看不到下游原始报文。</summary>
public sealed class AgentCapabilityOutputHint
{
    /// <summary>下游 JSON 里的字段名，如 qtyOnHand、orderNo。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>给规划器的一句说明。</summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>Capability 的 HTTP 绑定：经 Integration 网关调用下游。</summary>
public sealed class AgentCapabilityHttpBinding
{
    /// <summary>host = XW.Host；external = 客户/第三方 API（需 BaseUrl）。</summary>
    public string Adapter { get; set; } = "host";

    /// <summary>引用 Agent:AuthProfiles 中的 Code。</summary>
    public string? AuthProfile { get; set; }

    /// <summary>external 适配器必填；host 时可空（用 HostApi.BaseUrl）。</summary>
    public string? BaseUrl { get; set; }

    /// <summary>HTTP 方法。</summary>
    public string Method { get; set; } = "GET";

    /// <summary>路径模板，可含 {{param}}。</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Query 模板字典。</summary>
    public Dictionary<string, string> Query { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Body JSON 模板（非 GET/HEAD）。</summary>
    public string? BodyTemplate { get; set; }

    /// <summary>超时秒数。</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>GET 最大重试次数（仅瞬时错误）。</summary>
    public int MaxRetries { get; set; }

    /// <summary>
    /// 下游失败时是否回退 Stub。注意：4xx（含 401/403/400）运行时强制不回退，
    /// 避免假成功污染业务答复。
    /// </summary>
    public bool FallbackToStub { get; set; }
}

/// <summary>下游鉴权配置档（非权限中台）。</summary>
public sealed class AgentAuthProfileOptions
{
    public string Code { get; set; } = string.Empty;

    /// <summary>none | bearer | api_key_header | forward_caller</summary>
    public string Type { get; set; } = "none";

    public string? Token { get; set; }
    public string? HeaderName { get; set; }

    public static List<AgentAuthProfileOptions> DefaultProfiles() =>
    [
        new() { Code = "none", Type = "none" },
        new() { Code = "host-bearer", Type = "bearer", Token = string.Empty },
        new() { Code = "forward-caller", Type = "forward_caller" }
    ];
}

/// <summary>能力级权限策略配置（不是第二套用户中台）。</summary>
public sealed class AgentPolicyOptions
{
    /// <summary>
    /// true = 不校验 agent.* / 菜单映射，一律放行（仅本地联调）。
    /// 默认配置文件为 false。Development 环境可在 appsettings.Development.json 打开。
    /// 下游 Host 仍用请求里的 AccessToken 鉴权。
    /// </summary>
    public bool Bypass { get; set; }

    /// <summary>true：无权限声明则拒绝。Bypass=true 时忽略本项。</summary>
    public bool DenyByDefault { get; set; } = true;

    /// <summary>角色 → 能力权限码映射。</summary>
    public Dictionary<string, List<string>> RolePermissions { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Console/ERP 菜单权限码 → Agent 能力权限码（如 inventoryBalance → agent.inventory.read）。
    /// </summary>
    public Dictionary<string, List<string>> PermissionAliases { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
