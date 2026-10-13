using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XW.Agent.Application.Abstractions;
using XW.Agent.Application.Capability;
using XW.Agent.Application.Context;
using XW.Agent.Application.Entities;
using XW.Agent.Application.Identity;
using XW.Agent.Application.Integration;
using XW.Agent.Application.Intents;
using XW.Agent.Application.Knowledge;
using XW.Agent.Application.Llm;
using XW.Agent.Application.Options;
using XW.Agent.Application.Planning;
using XW.Agent.Application.Policy;
using XW.Agent.Application.Runtime;
using XW.Agent.Application.Runtime.Pending;
using XW.Agent.Application.Services;
using XW.Agent.Application.Telemetry;
using XW.Agent.Application.Tooling;

namespace XW.Agent.Application;

/// <summary>
/// Agent Application DI 入口：注册 Context / Policy / Integration / Knowledge / Capability / Planner / Runtime。
/// Host 侧调用 <see cref="AddXwAgentApplication"/> 完成装配。
/// </summary>
public static class AgentServiceCollectionExtensions
{
    /// <summary>注册 XW Agent 应用层全部服务与配置绑定（Agent 配置节）。</summary>
    public static IServiceCollection AddXwAgentApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AgentOptions>(configuration.GetSection(AgentOptions.SectionName));
        services.PostConfigure<AgentOptions>(NormalizeAgentOptions);

        // Context / Identity / Policy
        services.AddSingleton<IUserContextAccessor, AsyncLocalUserContextAccessor>();
        services.AddSingleton<IAgentTurnTelemetry, AsyncLocalAgentTurnTelemetry>();
        services.AddSingleton<IIdentityResolver, BearerIdentityResolver>();
        services.AddSingleton<IPermissionPolicy, ConfigurablePermissionPolicy>();
        services.AddSingleton<IApprovalGate, ConfirmApprovalGate>();

        // Session / Audit：配置了 Knowledge 连接串则落 PostgreSQL，否则内存（重启丢失）
        services.AddSingleton<InMemoryAgentSessionStore>();
        services.AddSingleton<PgAgentSessionStore>();
        services.AddSingleton<IAgentSessionStore>(sp =>
        {
            var cs = sp.GetRequiredService<IOptionsMonitor<AgentOptions>>()
                .CurrentValue.Knowledge.ConnectionString;
            if (string.IsNullOrWhiteSpace(cs))
            {
                sp.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("XW.Agent.Session")
                    .LogWarning(
                        "未配置 Agent:Knowledge:ConnectionString，会话使用内存存储（重启丢失待办和幂等键）。生产请配连接串并执行 docs/sql/xw_agent.postgresql.sql");
                return sp.GetRequiredService<InMemoryAgentSessionStore>();
            }

            return sp.GetRequiredService<PgAgentSessionStore>();
        });
        services.AddSingleton<InMemoryAgentAuditStore>();

        // Integration（Host / 客户 API Adapter）
        services.AddHttpClient(HttpIntegrationGateway.HttpClientName);
        services.AddSingleton<IIntegrationGateway, HttpIntegrationGateway>();

        // Knowledge / RAG：开关控制；仅 vector，关闭则跳过，不回退词法
        services.AddSingleton<IKnowledgeStore, FileKnowledgeStore>();
        services.AddSingleton<PlainTextDocumentExtractor>();
        services.AddSingleton<UnsupportedBinaryDocumentExtractor>();
        services.AddSingleton<IDocumentTextExtractor>(sp => new CompositeDocumentTextExtractor(
        [
            sp.GetRequiredService<PlainTextDocumentExtractor>(),
            sp.GetRequiredService<UnsupportedBinaryDocumentExtractor>()
        ]));
        services.AddSingleton<KnowledgePgDataSource>();
        services.AddSingleton<PgAgentAuditStore>();
        services.AddSingleton<IAgentAuditStore>(sp =>
        {
            var cs = sp.GetRequiredService<IOptionsMonitor<AgentOptions>>()
                .CurrentValue.Knowledge.ConnectionString;
            if (string.IsNullOrWhiteSpace(cs))
            {
                sp.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("XW.Agent.Audit")
                    .LogWarning(
                        "未配置 Agent:Knowledge:ConnectionString，审计使用内存存储（重启丢失）。生产请配连接串并执行 docs/sql/xw_agent.postgresql.sql");
                return sp.GetRequiredService<InMemoryAgentAuditStore>();
            }

            return sp.GetRequiredService<PgAgentAuditStore>();
        });
        services.AddSingleton<InMemoryAgentTurnStore>();
        services.AddSingleton<PgAgentTurnStore>();
        services.AddSingleton<IAgentTurnStore>(sp =>
        {
            var cs = sp.GetRequiredService<IOptionsMonitor<AgentOptions>>()
                .CurrentValue.Knowledge.ConnectionString;
            if (string.IsNullOrWhiteSpace(cs))
            {
                sp.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("XW.Agent.Turn")
                    .LogWarning(
                        "未配置 Agent:Knowledge:ConnectionString，轮次使用内存存储。生产请配连接串并执行 docs/sql/xw_agent.postgresql.sql");
                return sp.GetRequiredService<InMemoryAgentTurnStore>();
            }

            return sp.GetRequiredService<PgAgentTurnStore>();
        });
        services.AddSingleton<IKnowledgeVectorStore, PgVectorKnowledgeStore>();
        services.AddHttpClient(OpenAiCompatibleEmbeddingClient.HttpClientName, (sp, client) =>
        {
            var seconds = sp.GetRequiredService<IOptionsMonitor<AgentOptions>>()
                .CurrentValue.Knowledge.Embedding.TimeoutSeconds;
            client.Timeout = TimeSpan.FromSeconds(seconds <= 0 ? 60 : seconds);
        });
        services.AddSingleton<IEmbeddingClient, OpenAiCompatibleEmbeddingClient>();
        services.AddSingleton<IKnowledgeIngestService, KnowledgeIngestService>();
        services.AddSingleton<VectorKnowledgeRetriever>();
        services.AddSingleton<IKnowledgeRetriever, ConfigurableKnowledgeRetriever>();
        // FileKnowledgeStore / Lexical：仅保留 status 兼容，不再作为 Chat 检索回退
        services.AddSingleton<LexicalKnowledgeRetriever>();

        // Capability 注册中心 + 动态检索 + Tooling
        services.AddSingleton<FileSkillCatalog>();
        services.AddSingleton<ICapabilityRegistry>(sp => sp.GetRequiredService<FileSkillCatalog>());
        services.AddSingleton<ICapabilityCatalog>(sp => sp.GetRequiredService<FileSkillCatalog>());
        services.AddSingleton<ICapabilityRetriever, LexicalCapabilityRetriever>();
        services.AddSingleton<IAgentTool, KnowledgeSearchTool>();
        services.AddSingleton<IAgentTool, StubProductsSearchTool>();
        services.AddSingleton<IAgentTool, StubMaterialsSearchTool>();
        services.AddSingleton<IAgentTool, StubInventoryBalancesTool>();
        services.AddSingleton<IAgentTool, StubOrderFulfillmentQueryTool>();
        services.AddSingleton<IAgentTool, StubLowStockAlertTool>();
        services.AddSingleton<IToolRegistry, CapabilityToolRegistry>();
        // LLM：Microsoft.Extensions.AI IChatClient + 业务门面 Adapter（Planner/Intent/Reply 不改）
        services.AddSingleton<IAgentLlmRuntime, AgentLlmRuntime>();
        services.AddSingleton<IPromptCatalog, OptionsPromptCatalog>();
        services.AddHttpClient(OpenAiCompatibleChatClient.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
        });
        services.AddSingleton<IChatClient>(sp =>
        {
            var http = sp.GetRequiredService<IHttpClientFactory>()
                .CreateClient(OpenAiCompatibleChatClient.HttpClientName);
            var runtime = sp.GetRequiredService<IAgentLlmRuntime>();
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<OpenAiCompatibleChatClient>();
            var turnTelemetry = sp.GetRequiredService<IAgentTurnTelemetry>();
            IChatClient inner = new OpenAiCompatibleChatClient(http, runtime, turnTelemetry, logger);

            // MEAI 中间件：官方 OTel（与 Sprint 1 ActivitySource 并存）
            return new ChatClientBuilder(inner)
                .UseOpenTelemetry(sourceName: "XW.Agent.Llm")
                .Build(sp);
        });
        services.AddSingleton<IChatCompletionClient, ChatCompletionClientAdapter>();
        services.AddSingleton<IAgentLaneRouter, RuleBasedLaneRouter>();
        services.AddSingleton<LlmIntentRouter>();
        services.AddSingleton<RuleBasedIntentRouter>();
        services.AddSingleton<IIntentRouter, CompositeIntentRouter>();
        services.AddSingleton<IAgentPlanner, LlmAgentPlanner>();
        services.AddSingleton<LlmTaskPlanner>();
        services.AddSingleton<RuleBasedTaskPlanner>();
        services.AddSingleton<IAgentTaskPlanner, CompositeTaskPlanner>();
        services.AddSingleton<IProductEntityResolver, CapabilityProductEntityResolver>();
        services.AddSingleton<IReplyComposer, LlmReplyComposer>();

        services.AddSingleton<IPendingTaskHandler, PurchaseCreatePendingHandler>();
        services.AddSingleton<IPendingTaskDispatcher, PendingTaskDispatcher>();
        services.AddSingleton<IWorkflowExecutor, LinearWorkflowExecutor>();
        services.AddSingleton<IToolResultEvaluator, RuleBasedToolResultEvaluator>();
        services.AddSingleton<IAgentOrchestrator, AgentOrchestrator>();
        return services;
    }

    private static void NormalizeAgentOptions(AgentOptions opt)
    {
        if (opt.AuthProfiles.Count == 0)
        {
            opt.AuthProfiles = AgentAuthProfileOptions.DefaultProfiles();
        }
        else
        {
            opt.AuthProfiles = opt.AuthProfiles
                .GroupBy(p => p.Code, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .ToList();
        }

        // 兼容：HostApi.AccessToken → host-bearer
        if (!string.IsNullOrWhiteSpace(opt.HostApi.AccessToken))
        {
            var hostBearer = opt.AuthProfiles.FirstOrDefault(p =>
                string.Equals(p.Code, "host-bearer", StringComparison.OrdinalIgnoreCase));
            if (hostBearer is not null && string.IsNullOrWhiteSpace(hostBearer.Token))
            {
                hostBearer.Token = opt.HostApi.AccessToken;
            }
        }

        if (opt.Capabilities.Count == 0)
        {
            opt.Capabilities = AgentCapabilityDefinition.DefaultCatalog();
        }
        else
        {
            opt.Capabilities = opt.Capabilities
                .GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .ToList();
        }

        foreach (var cap in opt.Capabilities)
        {
            cap.NormalizeContract();
        }

        // 校验 AuthProfile 引用
        var authCodes = opt.AuthProfiles.Select(p => p.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var cap in opt.Capabilities.Where(c => c.Http is not null))
        {
            var profile = cap.Http!.AuthProfile;
            if (!string.IsNullOrWhiteSpace(profile) && !authCodes.Contains(profile))
            {
                // 降级为 none，避免启动即不可用
                cap.Http.AuthProfile = "none";
            }

            if (string.Equals(cap.Http.Adapter, "external", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(cap.Http.BaseUrl))
            {
                cap.Enabled = false;
            }
        }

        if (opt.Intents.Count == 0)
        {
            opt.Intents = AgentIntentDefinition.DefaultCatalog();
        }

        foreach (var cap in opt.Capabilities.Where(c => c.Enabled))
        {
            var intentCode = string.IsNullOrWhiteSpace(cap.IntentCode) ? cap.Code : cap.IntentCode!;
            var intent = opt.Intents.FirstOrDefault(i =>
                string.Equals(i.Code, intentCode, StringComparison.OrdinalIgnoreCase));
            if (intent is null)
            {
                opt.Intents.Add(new AgentIntentDefinition
                {
                    Code = intentCode,
                    Description = cap.Description,
                    ToolName = cap.Code,
                    Permission = cap.ResolvePermissions().FirstOrDefault() ?? string.Empty,
                    Permissions = cap.ResolvePermissions().ToList(),
                    Slots = cap.Parameters.Select(p => new AgentSlotDefinition
                    {
                        Name = p.Name,
                        Description = p.Description,
                        Required = p.Required
                    }).ToList()
                });
            }
            else
            {
                intent.ToolName ??= cap.Code;
                var resolved = cap.ResolvePermissions();
                if (string.IsNullOrWhiteSpace(intent.Permission) && resolved.Count > 0)
                {
                    intent.Permission = resolved[0];
                }

                if (intent.Permissions.Count == 0 && resolved.Count > 0)
                {
                    intent.Permissions = resolved.ToList();
                }

                if (intent.Slots.Count == 0 && cap.Parameters.Count > 0)
                {
                    intent.Slots = cap.Parameters.Select(p => new AgentSlotDefinition
                    {
                        Name = p.Name,
                        Description = p.Description,
                        Required = p.Required
                    }).ToList();
                }
            }
        }

        if (string.IsNullOrWhiteSpace(opt.Prompts.IntentSystem))
        {
            opt.Prompts.IntentSystem = AgentPromptOptions.DefaultIntentSystem;
        }

        if (string.IsNullOrWhiteSpace(opt.Prompts.IntentUserTemplate))
        {
            opt.Prompts.IntentUserTemplate = AgentPromptOptions.DefaultIntentUser;
        }

        if (string.IsNullOrWhiteSpace(opt.Prompts.ReplySystem))
        {
            opt.Prompts.ReplySystem = AgentPromptOptions.DefaultReplySystem;
        }

        if (string.IsNullOrWhiteSpace(opt.Prompts.ReplyUserTemplate))
        {
            opt.Prompts.ReplyUserTemplate = AgentPromptOptions.DefaultReplyUser;
        }

        if (string.IsNullOrWhiteSpace(opt.Prompts.PlannerSystem))
        {
            opt.Prompts.PlannerSystem = AgentPromptOptions.DefaultPlannerSystem;
        }

        if (string.IsNullOrWhiteSpace(opt.Prompts.PlannerUserTemplate))
        {
            opt.Prompts.PlannerUserTemplate = AgentPromptOptions.DefaultPlannerUser;
        }

        if (string.IsNullOrWhiteSpace(opt.Prompts.TaskPlanSystem))
        {
            opt.Prompts.TaskPlanSystem = AgentPromptOptions.DefaultTaskPlanSystem;
        }

        if (string.IsNullOrWhiteSpace(opt.Prompts.TaskPlanUserTemplate))
        {
            opt.Prompts.TaskPlanUserTemplate = AgentPromptOptions.DefaultTaskPlanUser;
        }

        if (opt.Planner.MaxSteps <= 0)
        {
            opt.Planner.MaxSteps = 6;
        }

        if (opt.Planner.Retrieval.TopK <= 0)
        {
            opt.Planner.Retrieval.TopK = 8;
        }

        if (opt.Knowledge.TopK <= 0)
        {
            opt.Knowledge.TopK = 3;
        }

        if (opt.Knowledge.MinScore < 0)
        {
            opt.Knowledge.MinScore = 0;
        }

        if (string.IsNullOrWhiteSpace(opt.Knowledge.Provider))
        {
            opt.Knowledge.Provider = "vector";
        }

        if (opt.Knowledge.ChunkSize <= 0)
        {
            opt.Knowledge.ChunkSize = 700;
        }

        if (opt.Knowledge.ChunkOverlap < 0)
        {
            opt.Knowledge.ChunkOverlap = 0;
        }

        if (opt.Knowledge.Embedding.Dimensions <= 0)
        {
            opt.Knowledge.Embedding.Dimensions = 1024;
        }

        if (string.IsNullOrWhiteSpace(opt.Knowledge.Embedding.EmbeddingsPath))
        {
            opt.Knowledge.Embedding.EmbeddingsPath = "/v1/embeddings";
        }

        if (opt.Knowledge.MaxUploadBytes <= 0)
        {
            opt.Knowledge.MaxUploadBytes = 20 * 1024 * 1024;
        }

        if (string.IsNullOrWhiteSpace(opt.Catalog.Mode))
        {
            opt.Catalog.Mode = "legacy";
        }

        // layered 且未配业务能力时，注入库存试点 + 将其余技术 Capability 投影为直绑业务能力
        if (opt.Catalog.IsLayered && opt.BusinessCapabilities.Count == 0)
        {
            SeedLayeredBusinessCatalog(opt);
        }

        foreach (var cap in opt.BusinessCapabilities)
        {
            cap.NormalizeContract();
        }

        var llm = opt.Llm;
        // Profiles 只认配置；不再用代码写死 Endpoint/Model 回填。
        // 若配置为空但有旧扁平字段，迁入一条 legacy。
        if (llm.Profiles.Count == 0
            && (!string.IsNullOrWhiteSpace(llm.BaseUrl) || !string.IsNullOrWhiteSpace(llm.Model)))
        {
            llm.Profiles =
            [
                new AgentLlmProfileOptions
                {
                    Code = "legacy",
                    Name = "Legacy flat config",
                    ProviderCode = "openai",
                    Endpoint = llm.BaseUrl ?? string.Empty,
                    ModelName = llm.Model ?? string.Empty,
                    ApiKey = llm.ApiKey ?? string.Empty,
                    Temperature = llm.Temperature ?? 0.1f,
                    UseJsonResponseFormat = false
                }
            ];
            llm.ActiveProfile = "legacy";
        }
        else if (llm.Profiles.Count > 0)
        {
            llm.Profiles = llm.Profiles
                .GroupBy(p => p.Code, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(llm.BaseUrl) || !string.IsNullOrWhiteSpace(llm.Model))
        {
            // 扁平字段若存在，合并进已有 ActiveProfile / local，而不是另插一份默认 Ollama
            var target = llm.Profiles.FirstOrDefault(p =>
                             string.Equals(p.Code, llm.ActiveProfile, StringComparison.OrdinalIgnoreCase))
                         ?? llm.Profiles.FirstOrDefault();
            if (target is not null)
            {
                if (!string.IsNullOrWhiteSpace(llm.BaseUrl))
                {
                    target.Endpoint = llm.BaseUrl!;
                }

                if (!string.IsNullOrWhiteSpace(llm.Model))
                {
                    target.ModelName = llm.Model!;
                }

                if (llm.ApiKey is not null)
                {
                    target.ApiKey = llm.ApiKey;
                }

                if (llm.Temperature.HasValue)
                {
                    target.Temperature = llm.Temperature.Value;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(llm.ActiveProfile) && llm.Profiles.Count > 0)
        {
            llm.ActiveProfile = llm.Profiles[0].Code;
        }
    }

    /// <summary>
    /// layered 默认投影：inventory.query → workflow；其余技术能力直绑为业务能力；
    /// 并从业务视图隐藏被 workflow 吸收的底层 API（products/materials.search、inventory.balances.query）。
    /// </summary>
    private static void SeedLayeredBusinessCatalog(AgentOptions opt)
    {
        var absorbed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "products.search",
            "materials.search",
            "inventory.balances.query"
        };

        if (!opt.Workflows.Any(w => string.Equals(w.Code, "wf.inventory.query.by_keyword", StringComparison.OrdinalIgnoreCase)))
        {
            opt.Workflows.Add(new AgentWorkflowDefinition
            {
                Code = "wf.inventory.query.by_keyword",
                Name = "按关键字查库存",
                Enabled = true,
                Input =
                [
                    new AgentCapabilityParameter
                    {
                        Name = "keywords",
                        Type = "string",
                        Required = false,
                        Description = "SKU 或品名（产品/物料）"
                    },
                    new AgentCapabilityParameter
                    {
                        Name = "productId",
                        Type = "string",
                        Required = false,
                        Description = "已解析的产品 Id"
                    },
                    new AgentCapabilityParameter
                    {
                        Name = "materialId",
                        Type = "string",
                        Required = false,
                        Description = "已解析的物料 Id"
                    }
                ],
                Steps =
                [
                    new AgentWorkflowStep
                    {
                        Id = "s1",
                        Tool = "products.search",
                        SkipIfVarPresent = "productId,materialId",
                        Arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["keywords"] = "{{keywords}}"
                        },
                        Assign = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["productId"] = "{{firstId}}"
                        },
                        OnEmpty = "continue"
                    },
                    new AgentWorkflowStep
                    {
                        Id = "s2",
                        Tool = "materials.search",
                        SkipIfVarPresent = "productId,materialId",
                        Arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["keywords"] = "{{keywords}}"
                        },
                        Assign = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["materialId"] = "{{firstId}}"
                        },
                        OnEmpty = "clarify"
                    },
                    new AgentWorkflowStep
                    {
                        Id = "s3",
                        Tool = "inventory.balances.query",
                        Arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["productId"] = "{{productId}}",
                            ["materialId"] = "{{materialId}}"
                        },
                        OnEmpty = "ok"
                    }
                ]
            });
        }
        else
        {
            // 热升级：旧种子把库存空列表当 fail，改为 ok（现存 0）
            var wf = opt.Workflows.First(w =>
                string.Equals(w.Code, "wf.inventory.query.by_keyword", StringComparison.OrdinalIgnoreCase));
            var balanceStep = wf.Steps.LastOrDefault(s =>
                s.Tool.Contains("inventory.balances", StringComparison.OrdinalIgnoreCase));
            if (balanceStep is not null
                && !string.Equals(balanceStep.OnEmpty, "ok", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(balanceStep.OnEmpty, "zero", StringComparison.OrdinalIgnoreCase))
            {
                balanceStep.OnEmpty = "ok";
            }
        }

        opt.BusinessCapabilities.Add(new AgentCapabilityDefinition
        {
            Code = "inventory.query",
            Name = "库存查询",
            Description = "按品名或 SKU 查询库存；自动检索产品或物料并查余额。",
            WhenToUse = "用户要查某个商品/物料还剩多少、库存是否充足，只给了品名或 SKU。",
            Domain = "inventory",
            Tags = ["库存", "现存", "SKU", "品名", "物料"],
            Aliases = ["查库存", "库存多少", "还有多少货"],
            Risk = "read",
            Permissions = ["agent.inventory.read"],
            Permission = "agent.inventory.read",
            IntentCode = "inventory.query",
            Enabled = true,
            Parameters =
            [
                new AgentCapabilityParameter
                {
                    Name = "keywords",
                    Type = "string",
                    Required = false,
                    Description = "SKU 或品名（产品/物料）"
                },
                new AgentCapabilityParameter
                {
                    Name = "productId",
                    Type = "string",
                    Required = false,
                    Description = "产品主键（可选，已解析时传入）"
                },
                new AgentCapabilityParameter
                {
                    Name = "materialId",
                    Type = "string",
                    Required = false,
                    Description = "物料主键（可选，已解析时传入）"
                }
            ],
            Entry = new AgentCapabilityEntry
            {
                Kind = "workflow",
                Target = "wf.inventory.query.by_keyword"
            }
        });

        foreach (var tech in opt.Capabilities.Where(c => c.Enabled && !absorbed.Contains(c.Code)))
        {
            opt.BusinessCapabilities.Add(new AgentCapabilityDefinition
            {
                Code = tech.Code,
                Name = tech.Name,
                Description = tech.Description,
                WhenToUse = tech.WhenToUse,
                Domain = tech.Domain,
                Tags = [.. tech.Tags],
                Aliases = [.. tech.Aliases],
                Risk = tech.Risk,
                Permissions = [.. tech.ResolvePermissions()],
                Permission = tech.Permission,
                ConfirmRequired = tech.ConfirmRequired,
                IntentCode = tech.IntentCode,
                Enabled = true,
                Parameters = tech.Parameters.Select(p => new AgentCapabilityParameter
                {
                    Name = p.Name,
                    Type = p.Type,
                    Required = p.Required,
                    Description = p.Description
                }).ToList(),
                Entry = new AgentCapabilityEntry
                {
                    Kind = "tool",
                    Target = tech.Code
                }
            });
        }
    }
}
