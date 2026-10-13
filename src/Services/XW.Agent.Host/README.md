# XW Enterprise Agent

按分层实现：Identity / Context / Runtime / Capability / Tooling / Policy / Integration / Knowledge / Audit。

## Console 集成

- Vue 后台：`client/` 经 Vite 代理 `VITE_APP_AGENT_API` → 本服务（默认 `http://localhost:5002`）。
- 悬浮窗 + 菜单页：智能对话 / 知识库 / 会话审计 / LLM 运行时。
- 菜单 SQL：已并入 `docs/sql/nx_devices_erp.mysql.sql`（执行后重新登录）。
- 请求自动带 Console JWT；办事时 Capability 可 `forward-caller` 调 Host。

## 最小演示（推荐）

见仓库根文档 [docs/agent-demo.md](../../../docs/agent-demo.md)：Host `:5295` + Agent `:5002` + `client` `pnpm dev`，用自然语言查库存/问售后。  
不要用 Aspire 动态端口做 Console 联调。

## 启动

```bash
dotnet run --project src/Services/XW.Agent.Host
```

- http://localhost:5002/swagger  
- `GET /api/agent/capabilities`  
- `GET /api/agent/llm`  

## 对话（带身份上下文）

```http
POST /api/agent/chat
Authorization: Bearer <可选-企业SSO-Token>
Content-Type: application/json

{
  "message": "查库存 物料Id 10086",
  "tenantId": "t001",
  "roles": ["warehouse"],
  "permissionCodes": ["agent.inventory.read"],
  "confirmed": false
}
```

也可把 Token 放在 body 的 `accessToken`。`forward-caller` AuthProfile 会把它转给 Host。

## 鉴权怎么落地（不另搞权限中台）

1. **入口**：企业 SSO Token / 显式 roles + permissionCodes  
2. **Policy**：默认 `Bypass=false`、`DenyByDefault=true`，空身份不能调能力。`dotnet run` 使用 Development，`appsettings.Development.json` 里 `Bypass=true` 便于本机联调。`RolePermissions` 把角色映射到能力权限码  
3. **下游**：Capability.Http.AuthProfile  
   - `forward-caller`：转发用户 Token  
   - `bearer` / `api_key_header`：服务账号或客户 API Key  
   - `none`：内网开放  
4. **Approval**：`ConfirmRequired=true` 时需 `confirmed=true`  
5. **Audit**：每轮对话写入 PostgreSQL 表 `agent_audit_record`（与知识库同连接串 `Agent:Knowledge:ConnectionString`）；未配连接串时回退内存（仅联调）。先执行 `docs/sql/xw_agent.postgresql.sql`。查询：`GET /api/agent/sessions/{sessionId}/audit`。轮次回放（失败也写）在 `agent_turn`，查询 `GET /api/agent/sessions/{sessionId}/turns`。已有库需要再执行一次 SQL 以创建 `agent_turn`。

## 扩展能力（Skill 文件）

按业务域在 `skills/{domain}/` 下一接口一 JSON。保存后热加载，**不要**把 Skill 堆进 `appsettings.json`。

```
skills/inventory/products.search.json
skills/purchase/order.create.product.json
skills/knowledge/search.json
skills/device/cooking.tasks.list.json
```

`Agent:Skills:RootPath` 默认 `skills`，`ReloadOnChange=true`。确认：`GET /api/agent/capabilities`。

外部系统：Skill 里 `Http.Adapter=external` + `BaseUrl` + `AuthProfiles`。

## 健壮性

- 下游超时 / GET 有限重试  
- 失败可 `FallbackToStub`（仅联调）  
- LLM 20s 超时，失败可回退规则意图  
- 密钥不进日志 URL  

## 编排（高级玩法）

1. 在 `skills/{业务域}/` 增加一条 JSON（一接口一文件），按 [Capability 标准](../../docs/capability-standard.md) 填写语义 / Domain / Tags / Http。  
2. `Planner.Retrieval.Enabled=true`：先动态检索 TopK，再交给 LLM Planner。  
3. Runtime：Retrieve → Plan → Authorize → Execute → Observe。  
4. **不要**写 C# 硬编码多接口流水线；**不要**把全量 Skill 塞进 Planner。

检索预览：`GET /api/agent/capabilities/search?q=查库存`。

## 知识库（向量 / pgvector）

生产路径：上传文件 → 切块 → Embedding HTTP → PostgreSQL，**不维护词库**。

1. 执行 `docs/sql/xw_agent.postgresql.sql`  
2. 配置 `Agent:Knowledge:ConnectionString` + `Embedding`  
3. `POST /api/agent/knowledge/upload`（`multipart file`）  
4. `GET /api/agent/knowledge/search?q=...`

说明：[agent-rag-vector.md](../../docs/agent-rag-vector.md)

## Sprint 1：Agent Trace

- ActivitySource：`XW.Agent`  
- Meter：`xw-agent`（聊天、工具、权限拒绝、待确认、LLM 失败、耗时和 Token）。与 Trace 共用控制台或 OTLP 导出  
- 开发默认控制台导出；OTLP：`Telemetry:OtlpEndpoint`  
- 路线图：[agent-ops-sprints.md](../../docs/agent-ops-sprints.md)

一次 chat 可见：`agent.chat` → `retrieve` / `plan` / `llm` / `execute` / `integration`。

## Sprint 2：IChatClient

- `IChatClient`（MEAI）+ `ChatCompletionClientAdapter`  
- Planner/Intent/Reply 仍用 `IChatCompletionClient`，业务零改动  
- OTel source：`XW.Agent.Llm`

## Prompt 约束

按车道拆分（`Agent:Prompts`）：
- **Knowledge**：`KnowledgeReply*`（仅 RAG 作答）
- **Action**：`Planner*` + `Reply*`（编排 API + 基于 Tool 事实回复）
- **Intent***：仅 Legacy 细粒度分类

粗分流由 Lane Router 完成，Prompt 不再负责「该不该调 API」。  
查看：`GET /api/agent/prompts`。

## Sprint 3：Registry

- `Permissions: string[]`（AND）；兼容单字段 `Permission`
- `domain` / `risk` / `confirmRequired` / `whenToUse` / `relatedCodes`

## Sprint 4：Aspire

```bash
dotnet run --project src/Aspire/XW.AppHost --launch-profile http
```

一键：`xw-host` + `xw-agent` + Dashboard Trace。说明见 `src/Aspire/XW.AppHost/README.md`。
