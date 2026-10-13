# 可配置审批引擎 — 任务计划

对照文档：[架构图](approval-engine.html) · [建表脚本](../sql/approval.mysql.sql)

模块：`XW.Approve`（Domain / Application.Contracts / Application / Infrastructure / API）  
口径不变：复用 `sys_user` / `sys_organization` / `sys_role`；schema 只走手工 SQL；运行时只有 `ApproveInstance` 一个聚合根。

---

## 现状


| 层                     | 状态      | 说明                                                      |
| --------------------- | ------- | ------------------------------------------------------- |
| Domain                | **已完成** | 实例聚合、选线、找人、会签/或签/票决、历史；常量 PascalCase；已编过                |
| Application.Contracts | 空壳      | 无 DTO / 接口                                              |
| Application           | 空壳      | 无服务                                                     |
| Infrastructure        | 空壳      | 无 EF 映射、仓储                                              |
| API                   | 空壳      | 无控制器                                                    |
| Host.API              | 未接入     | `ModuleCatalog`、csproj 均未引用 Approve                     |
| 库表                    | 脚本已写    | `docs/sql/approval.mysql.sql`，需在 `nx_devices_erp` 上手动执行 |
| 流程配置                  | 未做      | 尚无 P0 采购流程种子数据                                          |
| 业务接入                  | 未做      | 采购单等业务尚未调用审批                                            |


---



## 约束（全程不要破）

1. 不新建用户 / 部门 / 角色表；审批人指向现有 RBAC。
2. 不在启动 HostedService 里跑建表 SQL。
3. 部门经理 = `RoleInDept` + `DEPT_MANAGER` + 单据 `DeptId`；部门主管 = `RoleInDept` + `DEPT_SUPERVISOR` + 发起人部门；`GM` / `FINANCE` 只按 `Role`。
4. 下一节点按单据类型 + 表单快照，不按「这个人还属于哪些部门」。
5. 本期单人单部门（`sys_user.deptid`）。
6. 状态/类型取值 PascalCase，与 Domain 常量一致（`Running`、`OrSign`、`RoleInDept`…）。
7. 业务模块只依赖 `XW.Approve.Application.Contracts`，禁止引用 Approve.Domain。

---



## 阶段总览

```text
P0 可演示一条采购审批
  1 执行 SQL + 种子流程
  2 Infrastructure（落库）
  3 Application（提交 / 待办 / 通过 / 驳回 / 撤销）
  4 API + 挂进 Host
  5 采购单接入（提交 + 结束回调）
  6 领域测试 + 一条手工联调

P1 可配置
  流程/字段/绑定的后台 CRUD（仍无画布）
  菜单权限
  合同 / 项目 / 预算复用同一引擎

P2 以后再说
  流程设计器、转交/加签、一人多部门、票决细化
```

建议按编号顺序做。1 可与 2 并行（SQL 不挡写代码）。5 依赖 3、4。

---



## 1. 库表与种子（手工 SQL）

**产出：** 库里有 `appr_`* 表，且能跑通一条「采购审批」定义。

- [ ] 在 `nx_devices_erp` 执行 `docs/sql/approval.mysql.sql`（已建过则核对本期 PascalCase 默认值）
- [ ] 确认 `sys_role` 已有：`DEPT_MANAGER` / `DEPT_SUPERVISOR` / `FINANCE` / `GM` / `STRATEGY_OFFICE`
- [ ] 把真实用户挂到上述角色，并核对 `sys_user.deptid`
- [ ] 新增种子脚本 `docs/sql/approval-p0-purchase.mysql.sql`（可重复执行），至少包含：
  - `appr_business_field`：采购金额 `Amount`、单据部门 `DeptId` 等
  - `appr_process_def`：`Purchase.Default` 版本 1，`Status = Active`
  - 节点：`Start` → 部门经理（`RoleInDept` + `DeptFrom=Business`）→ 财务（`Role`）→ 可选总经理（金额条件）→ `End`
  - 连线 XOR：金额大于阈值走 GM，否则默认结束
  - `appr_process_binding`：`BusinessType = Purchase` 的默认绑定
- [ ] 不要把种子写进 C# 启动逻辑

**验收：** Navicat 能看到完整流程图数据；角色上有人。

---



## 2. Infrastructure

对照 `XW.SupplyChain.Infrastructure`。

- [x] `ApproveDbContext` + `EntityInfo`
  - 表名：`appr_instance` / `appr_instance_node` / `appr_task` / `appr_history`
  - 配置表：`appr_process_def` / `appr_process_node` / `appr_process_transition` / `appr_condition_group` / `appr_condition_item` / `appr_process_binding` / `appr_resolver` / `appr_business_field`
- [x] 实体配置
  - `ApproveInstance`：`HasMany` Nodes / Tasks / Histories；`BusinessType + BusinessId + Status` 索引
  - 实例有 `IConcurrency.RowVersion`，**表上没有该列**：配置里 `Ignore(x => x.RowVersion)`，并发先用 `ModifyTime` 或后续再加列
  - Node / Task / History 无 `CreateBy`/`ModifyBy`：不要当 `IFullAuditInfo` 映射
- [x] `ApproveInstanceRepository` 实现 `IApproveInstanceRepository`
  - `GetForWriteAsync`：Include 节点、任务、历史
  - `FindRunningAsync(businessType, businessId)`
  - Insert / Update 一次 `SaveChanges`
- [x] `ApproveProcessGraphRepository` 实现 `IApproveProcessGraphRepository`
  - 只加载 `Active` 版本的节点、连线、条件、解析器，组装 `ProcessRuntimeGraph`
- [x] 组织快照读取（可放 Infrastructure Readers）
  - 角色 Code → Id（`sys_role`）
  - 角色 Id → 用户 Id 列表（`sys_role_user_relation`）
  - 用户 Id → `deptid`（`sys_user`）
  - 组装 `AssigneeResolveContext`
- [x] 配置实体用模块内 `AddXWModuleEfBasicRepository`（ProcessDef、Binding、BusinessField 等）
- [x] `DependencyRegistrar`：注册仓储、DbContext

**验收：** 能把一次 `ApproveInstance.Start(...)` 整图写入四张运行时表。

---



## 3. Application.Contracts + Application

Application 只做：鉴权上下文、加载图与组织快照、调聚合、落库、查列表。**不写流转 if/else。**

### 3.1 Contracts

- [x] `IApproveInstanceService`
  - `SubmitAsync`（业务类型、业务主键、标题、表单快照）
  - `ApproveAsync` / `RejectAsync` / `CancelAsync`
  - `GetAsync`（详情 + 节点 + 历史）
  - `GetMyTasksPagedAsync`
  - `GetMySubmittedPagedAsync`
- [x] DTO：提交入参、处理入参、实例详情、待办列表项
- [x] 业务结束回调接口（Contracts 内，供采购等实现或事件）：`IApproveCompletedHandler` 或领域事件  
  实例变为 `Approved` / `Rejected` / `Cancelled` 时通知业务模块改自己的状态
- [x] P0 不做流程设计器 DTO；配置靠 SQL 种子



### 3.2 Application

- [x] `ApproveInstanceService`
  1. 同一 `BusinessType + BusinessId` 已有 `Running` → 拒绝
  2. 按绑定选 `ProcessDefId`（P0 可先取该业务默认绑定）
  3. 加载 `ProcessRuntimeGraph` + `AssigneeResolveContext`
  4. 快照写入 `DeptId`（单据部门）和发起人部门（提交人 `deptid`）
  5. `ApproveInstance.Start(...)` → `InsertAsync`
- [x] 通过 / 驳回：`GetForWriteAsync` → 聚合方法 → `UpdateAsync`；捕获 `ApprovalDomainException` → 400
- [x] 撤销：仅发起人（聚合已校验）
- [x] 待办查询：按当前用户 + `Pending` 任务
- [x] 提交后 / 处理后：若实例已结束，调用业务回调（可先空实现，采购接入时补）
- [x] `DependencyRegistrar`（Application 层）

**验收：** 不写控制器也能用测试或临时调用提交一单，库中出现实例、节点、待办、`Submit` 历史。

---



## 4. API 与 Host 接入

- [x] `ApproveInstancesController`：提交、详情、我发起的
- [x] `ApproveTasksController`：我的待办、通过、驳回
- [x] 路由：`{ApiSurfaces.ConsoleRoutePrefix}/approve/...`，继承 `ConsoleApiController`
- [x] `XW.Host.API.csproj` 增加 Approve 的 Infrastructure / Application / API 引用
- [x] `ModuleCatalog` 增加 Approve 的 Registrar 与 API Assembly
- [x] 菜单与权限码（手工 SQL，写入 `sys_menu` / 角色菜单）：提交、待办、详情  
  权限只控制「能不能进页面」，不决定谁是审批人

**验收：** Swagger 能调提交 / 待办 / 通过；未登录 401。

---



## 5. P0 业务接入（采购）

第一单建议采购，字段简单，且 SupplyChain 已有聚合。

- [ ] 采购确认或「提交审批」时组快照：`Amount`、`DeptId`、单号、标题
- [ ] 调 `IApproveInstanceService.SubmitAsync`，`BusinessType = Purchase`，`BusinessId = 采购单 Id`
- [ ] 审批中禁止再改单（采购侧状态）
- [ ] 实现结束回调：`Approved` → 采购单确认/生效；`Rejected` / `Cancelled` → 回到草稿或关闭
- [ ] SupplyChain **只引用** `XW.Approve.Application.Contracts`

合同 / 项目 / 预算：复制绑定 + 字段定义即可，P0 不做。

**验收：** 采购提交 → 部门经理待办 → 通过 → 财务待办 → 通过（或金额大走总经理）→ 采购单状态变化。

---



## 6. 测试与联调

- [ ] Domain 单测（不必起库）：`tests/XW.Approve.Tests`
  - 开始节点自动往下
  - 或签一人过、其余 Skip
  - 会签未齐不推进
  - 金额条件走 GM
  - `RoleInDept` 只命中单据部门
  - 有其他人时跳过发起人
  - 结束节点无人可派即 `Approved`
- [ ] 手工联调清单
  - 发起人自己是部门经理且还有别人 → 待办不包含自己
  - 发起人是唯一经理 → 待办仍是自己
  - 会签两人同时点：以第二次 `ModifyTime`/重载为准，不出现双下一节点（P0 接受重试）
- [ ] 不把 SQL 放进测试启动

---



## P1（P0 演示通过后再排）

- [ ] 流程定义 / 节点 / 连线 / 条件 / 解析器 / 绑定 / 业务字段的后台 CRUD（仍无画布）
- [ ] 发布新版本不影响在途实例（已按 `ProcessDefId` 冻结，需禁止改 Active 图，只允许新版本）
- [ ] 绑定条件（`OwnerType = Binding`）按快照选流程，不只走 IsDefault
- [ ] 合同、项目、预算：各一份 field + binding + 种子流程
- [ ] 我已处理 / 抄送（`Cc`）列表
- [ ] 乐观并发：给 `appr_instance` 加版本列或统一 Ignore 策略文档化

---



## P2（明确不做，直到有需求）

- 流程可视化设计器
- 转交 / 加签 / 委托（表字段已预留）
- `sys_user_organization_relation` 一人多部门
- 自动跳过空节点、超时、催办
- 把 `ProcessDef` 升级成聚合根（仅当「一次保存整张图」时）

---



## 建议顺序与依赖

```text
1 SQL + 种子  ─────────────────────┐
2 Infrastructure  ← Domain 已齐     ├→ 3 Application → 4 API/Host → 5 采购接入 → 6 测试联调
                                   │
组织快照 Reader 可跟 2 一起写 ──────┘
```

P0 完成标志：控制台能提交采购审批，待办能通过/驳回，采购单状态跟着变，全程不改审批 Domain 规则。