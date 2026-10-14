# Agent 底座开工

按 `docs/agent/agent-architecture.md` 施工。本文只排这一阶段的做法和完成标准。

## 不对现有业务负责

采购、库存、订单、预警、设备，以及编排器里为它们写的分支，都不在这次交付里。这些业务后面会移除。

因此：

- 不修复它们，也不为了「还能对话查库存、能走采购待办」去补代码。
- 不把它们登记成新的作业处理器，不把技能 JSON 迁进业务包。
- 底座改造碰到只为这些业务服务的代码时，删掉。不为了少改而保留。
- 对话循环可以继续留在现有工程里，但其中的业务分支不再维持。

下面这些视为业务，不是底座：

| 位置 | 内容 |
| --- | --- |
| `XW.Agent.Host/skills/` 下除 `knowledge/` 以外的 JSON | 库存、采购、订单、预警、设备 |
| `Runtime/Pending/PurchaseCreatePendingHandler.cs` | 采购待办 |
| `PendingTaskSnapshot` 中的采购槽位、文案、幂等键 | 采购状态 |
| `Entities/CapabilityProductEntityResolver.cs`、`ProductNameNormalizer.cs` | 品名解析 |
| `DependencyRegistrar.SeedLayeredBusinessCatalog` | 库存工作流种子 |
| `LinearWorkflowExecutor` 里的 `qtyOnHand`、`IsInventoryBalancesStep` | 库存结果解释 |
| `RuleBasedTaskPlanner`、`TaskPlanStepCatalog` 里的库存 / 采购意图 | 业务句式 |
| `LexicalCapabilityRetriever.DomainHints` | 行业词表 |
| `Tooling/StubTools.cs` | 库存、订单等模拟工具 |
| 编排器里写死的采购取消文案和 `purchase_create` | 业务分支 |

`skills/knowledge/` 和知识检索实现属于底座，保留并按下面的步骤改。

## 这一阶段要交出来的

五步按顺序做。前一步的完成标准没达到，不开始下一步。

### 1. 立契约

新建 `XW.Agent.Contracts`。现有 `XW.Agent.Application` 引用它。

放进契约的只有：

- 资料集编号、命中（含文档 Id、切块 Id）
- 检索接口、入库接口
- 知识库连接和 Embedding 所需的配置类型
- 工具说明
- 作业请求、作业结果、`IJobHandler`

整个 `AgentOptions` 不搬。不在契约里放采购、库存或任何业务 DTO。

完成标准：契约项目没有业务类型；解决方案能编译。

### 2. 检索必须带资料集

仍先改现有检索代码，表结构不动。`domain` 列就是资料集编号。

- 查询条件改为 `domain` 属于传入的编号列表。
- 列表为空时拒绝，不执行全库查询。拒绝和「没有命中」要能区分。
- 命中带上文档 Id。`document_id` 列已经存在，只把它查出来。
- 入库缺少资料集编号时拒绝，不再默认写成 `general`。已入库且 `domain = general` 的旧数据，调用方传入 `general` 时仍然能查到。
- `GET /api/agent/knowledge/search` 增加资料集参数。缺参数返回 400。该接口直接调用检索接口，不经过编排器。
- 对话里的 `knowledge.search` 只使用调用方写入会话的资料集编号，不采用模型填写的编号。会话里没有编号则这次工具调用失败，不扫全库。
- 词法检索不作为回退。

完成标准：不带资料集的检索不会发出全库 SQL；带资料集的命中包含文档 Id。

### 3. 检索实现进入 Infrastructure

新建 `XW.Agent.Infrastructure`，只引用 Contracts。

迁入 Embedding、pgvector、入库、按资料集检索。知识库调试 API 放到 `XW.Agent.Host`，只依赖契约里的检索接口。

会话库、审计库这一步不搬。`PgVectorKnowledgeStore` 不再留在对话工程里。

完成标准：依赖方向是 Host → Infrastructure → Contracts。对话工程不再包含向量检索实现。

### 4. 进程内作业分发

按 `kind` 把信封交给已登记的 `IJobHandler`。`input` 和 `output` 原样传递，分发器不解析业务 JSON。

Host 提供一个同步调用入口，请求和响应就是作业信封。零个处理器也可以启动。未知 `kind` 返回 `failed`。

不注册任何业务处理器，不接 CAP。

完成标准：用一个只回显 `input` 的临时处理器能走通信封；核对后删掉这个临时处理器。正式代码里不留下示例业务。

### 5. 消息适配

CAP 只做运输：收到信封就调用第 4 步的分发器，再按 `replyTo` 发回结果。

不新增消息中间件，不订阅审批，不在适配器里判断业务。

完成标准：传输层没有检索 SQL，也没有业务字段。关掉 CAP 时，第 2 步的调试检索和第 4 步的进程内调用仍然可用。

## 这一阶段不做

- 不建立 `XW.Agent.Business`。
- 不拆模型适配、通用工具执行和整段对话循环。它们继续留在现有工程，其中的业务分支按上文删除，不另做一轮业务改造。
- 不把现有业务接到新的作业或待办扩展点上。
- 不改知识库表，不写 `INSERT` / `CREATE TABLE`。
- 不为某个具体流程增加 `kind`。

模型、工具执行、对话循环等待检索和作业两条调用链稳定后，再按同一边界收进契约。那是下一阶段。
