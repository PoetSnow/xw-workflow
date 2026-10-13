# 环节类型：审批 / 协作 / 知会 / 任务（开工稿）

组织、任职、岗位找人见 [org-rbac-approve-proposal.md](./org-rbac-approve-proposal.md)。  
本文只补**环节类型**和**协作交付物**。图、条件线、实例、待办、历史、岗位解析都留。

原则：**每个环节先选类型，再按类型渲染办理页、判断完成、决定怎么往下走。**  
待办还是待办，点进去看到的东西不一样。

---

## 1. 现状

| 层 | 现在 | 缺口 |
|---|---|---|
| 图 | 可分叉（条件线 + 默认线），不是只能直线 | 不缺并行网关也能画混合流 |
| 节点 | 只有 `Start` / `Approve` / `End` | 中间环节一律同意/拒绝 + 意见 |
| 多人 | 或签 / 会签 / 票决 | 只改「怎么算过」，不改界面 |
| 知会 | `appr_task.tasktype` 留了 `Cc` | 设计器和办理都没做 |
| 协作 / 任务 | 无 | 不能交材料、不能拆子任务 |
| 条件字段 | `appr_business_field`（如 `totalamount`、`DeptId`） | 管走哪条线，不是协作表单 |

当前办理模型：所有工作环节 = 待办 + 审批按钮 + 意见。

---

## 2. 目标模型

```
每个环节先选类型
  ├─ 审批型 Approve      → 待办 + 同意/拒绝 + 意见
  ├─ 协作型 Collaborate  → 待办 + 设计器配的字段/附件 + 完成
  ├─ 知会型 Notify       → 通知 + 已读
  └─ 任务型 Task         → 第一期按协作型做（几条待办，全部完成）
```

找人仍用岗位解析：`PositionFromDocDept` / `PositionInFixedDept` / `User` / `Submitter`。

「并行」默认是**一个环节里几个人同时干活**（会签那套人齐了再走），不是两股流程再汇合。第一期不做并行网关。

---

## 3. 混合流（验收场景）

```
[发起]
   │
   ▼
□ 上级审批 【审批型】
   ▼
□ 预算协作 【协作型】
   ├┈ 财务 → 核预算（自己的字段清单）
   └┈ 采购 → 找供应商（另一份清单）
      完成规则：两方都完成
   ▼
□ 老板审批 【审批型】
   ▼
□ 知会法务 【知会型】  已读后往下（按串行画法）
   ▼
□ 执行派发 【任务型】  第一期同协作：行政 / 采购 / 市场，全部完成
   ▼
[结束]
```

图本身是一条链。财务和采购的「并行」在节点内部。

---

## 4. 已定结论

| # | 题目 | 结论 |
|---|---|---|
| 1 | 字段谁配 | 流程设计器配，不按流程让开发写页面 |
| 2 | 协作结果怎么存 | 待办 `payload` JSON + `appr_task_file` 附件 |
| 3 | 要不要一流程一张业务表 | 不要。主数据仍在业务单（采购单等） |
| 4 | 条件字段 vs 协作字段 | 分开。条件读单据快照；协作清单挂节点/解析器 |
| 5 | 财务和采购清单 | 可以不同。节点默认一份，每方可覆盖 |
| 6 | 驳回历史 | 已有 `appr_history` + 再提交新一轮。必须留，不覆盖旧轮 |
| 7 | 驳回策略（第一期） | 整单 `Rejected`，发起人改完再提交新实例 |
| 8 | 点名退回协作某个人 | 不做。上一环整环重开以后再说 |
| 9 | 知会是否挡下一环 | 第一期按图串行：已读才往下 |
| 10 | 任务型 | 第一期机制同协作型，办理文案不同即可 |
| 11 | 表单设计器 | 不要拖拽。固定字段类型 + 清单编辑 |
| 12 | 统计入业务表 | 以后要按科目汇总再抽。现在不建预算表 |

---

## 5. 字段清单（设计器）

和 `appr_business_field` 无关。那是单据条件。这里是**这一环这个人要交的**。

节点默认 `formschema`；`appr_resolver.formschema` 非空则覆盖（财务一份、采购一份）。

```json
{
  "fields": [
    { "key": "budgetAmount", "name": "预算金额", "type": "Number", "required": true },
    { "key": "budgetSubject", "name": "预算科目", "type": "Enum", "required": true, "options": ["差旅", "招待", "采购"] },
    { "key": "remark", "name": "说明", "type": "Text", "required": false }
  ],
  "allowAttachment": true,
  "attachmentRequired": false
}
```

第一期 `type`：`String`（单行）、`Text`（多行）、`Number`、`Date`、`Enum`（`options` 必填）。  
`key`：同一份 schema 内唯一，`^[A-Za-z][A-Za-z0-9_]{0,63}$`。

审批型、知会型：`formschema` 为空。开始/结束：无清单。

---

## 6. 办理与完成

| 类型 | `nodetype` | 待办 `tasktype` | 办理 | 完成规则 | 历史 `action` |
|---|---|---|---|---|---|
| 审批 | `Approve` | `Approve` | 同意 / 拒绝 + 意见 | 沿用 Single / OrSign / AndSign / Vote | `Approve` / `Reject` |
| 协作 | `Collaborate` | `Complete` | 按清单填表 + 附件 + 完成 | 该环待办都 `Complete`（会签） | `Complete` |
| 知会 | `Notify` | `Read` | 已读 | 该环待办都 `Read` 再往下 | `Read` |
| 任务 | `Task` | `Complete` | 同协作 | 同协作 | `Complete` |

提交校验：必填字段、数字/日期格式、`attachmentRequired` 时至少一条附件。  
`payload` 只存字段值，不存文件二进制：

```json
{ "budgetAmount": 12000, "budgetSubject": "采购", "remark": "Q2 设备" }
```

驳回：写 `appr_history`（人、意见、时间）；`snapshot` 带当时 `payload`。附件留在旧任务上。新一轮新实例，时间线按轮次看。

---

## 7. 数据表

库：`nx_devices_erp`。列名小写。可重复执行的增量脚本，单独放 `docs/sql/`，不改业务主表。

### 7.1 改 `appr_process_node`

```sql
-- nodetype：Start / Approve / Collaborate / Notify / Task / End
ALTER TABLE `appr_process_node`
  ADD COLUMN `formschema` json NULL COMMENT '本环默认交付清单' AFTER `rejectrulevalue`;
```

`nodetype` 现宽 16，`Collaborate` 正好 11。注释改为含新类型。存量行仍是 `Approve`。

### 7.2 改 `appr_resolver`

```sql
ALTER TABLE `appr_resolver`
  ADD COLUMN `formschema` json NULL COMMENT '覆盖节点默认清单；空则用节点 formschema' AFTER `resolverext`;
```

找人字段不动。

### 7.3 改 `appr_instance_node`

```sql
ALTER TABLE `appr_instance_node`
  ADD COLUMN `nodetype` varchar(16) NULL COMMENT '提交时从定义拷贝' AFTER `nodename`;
```

提交时写入。已发布版本改定义不影响在途单。

### 7.4 改 `appr_task`

```sql
ALTER TABLE `appr_task`
  ADD COLUMN `payload` json NULL COMMENT '本待办提交的字段 JSON' AFTER `comment`;
```

`tasktype` 实际取值：`Approve` / `Complete` / `Read`。存量审批任务仍是 `Approve`。

### 7.5 新建 `appr_task_file`

```sql
CREATE TABLE IF NOT EXISTS `appr_task_file` (
  `id` bigint NOT NULL,
  `taskid` bigint NOT NULL COMMENT '→ appr_task.id',
  `instanceid` bigint NOT NULL COMMENT '→ appr_instance.id',
  `filename` varchar(256) NOT NULL,
  `contenttype` varchar(128) NOT NULL DEFAULT '',
  `filesize` bigint NOT NULL DEFAULT 0,
  `storagekey` varchar(512) NOT NULL COMMENT '对象存储路径或本地键',
  `createby` bigint NOT NULL DEFAULT 0,
  `createtime` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`id`),
  INDEX `ix_appr_task_file_task` (`taskid`),
  INDEX `ix_appr_task_file_instance` (`instanceid`)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci COMMENT = '协作/任务待办附件';
```

文件存哪套存储，开工时跟现有上传对齐，本文不另起存储服务。

### 7.6 不改

- `appr_instance`、`appr_history` 结构（历史继续追加；`action` 增加 `Complete` / `Read`）
- `sc_purchase_order` 等业务表
- 不为「预算」单独建表

---

## 8. 代码要动（范围）

**后端 Approve**

- `ApproveNodeType` 增加 `Collaborate` / `Notify` / `Task`
- `ApproveTaskType`：`Complete` / `Read`（可保留未用的 `Cc` 或删掉，开工时只留实际值）
- `ApproveHistoryAction` 增加 `Complete` / `Read`
- 设计器保存：校验类型；协作/任务的 schema；审批型禁止当角色用的旧解析器（已删）
- 派任务：按节点类型写 `tasktype`；拷贝 `nodetype` 到实例节点
- 办理接口：审批仍同意/拒绝；协作/任务 `Complete` + payload + 文件；知会 `Read`
- 完成判定：协作/任务按会签（全 Complete）；知会全 Read
- 时间线：按类型出按钮和已交内容

**前端**

- 设计器：节点类型下拉；协作/任务编辑字段清单；解析器可覆盖清单
- 办理：按 `nodetype` / `tasktype` 换页（同意拒绝 / 动态表单 / 已读）
- 时间线：展示 payload 摘要和附件名

**不改**

- 岗位、任职、采购 `DeptId`
- XOR 条件、到达才激活待办、提交冻人

---

## 9. 落地阶段

1. **脚本**  
   第 7 节 ALTER + `appr_task_file`。单独文件，可重复执行。
2. **类型接通（无表单）**  
   设计器能选四种类型。知会已读。协作/任务先当「完成」按钮（无字段也能过）。审批行为不变。
3. **清单 + JSON**  
   设计器编字段；办理按清单渲染；写入 `payload`。
4. **附件**  
   `appr_task_file` + 上传下载。
5. **采购混合示例（可选）**  
   一条已发布示例流：经理审批 → 财务/采购协作 → 结束。用来验收，不代替真实业务配置。

点名回退、并行网关、拖拽表单、协作字段入业务表：不在本开工范围。

---

## 10. 业务侧开工前要准备

1. 选定 1 条要画的混合流和各环节类型。  
2. 写清每一方字段表（key / 名称 / 类型 / 必填 / 选项 / 附件）。  
3. 确认组织 `01～04` 已执行、流程已按岗位发布。  
4. 驳回按「整单结束再提」验收，不要按点名退回验收。

---

## 11. 和现网文档的关系

| 文档 | 管什么 |
|---|---|
| [org-rbac-approve-proposal.md](./org-rbac-approve-proposal.md) | 组织 / 任职 / 岗位找人 / 单据 DeptId |
| [instance-timeline.md](./instance-timeline.md) | 当前进度 vs 历史轮次 |
| **本文** | 环节类型、设计器清单、payload、附件表、完成规则 |

组织层做完再动本文。两者不要缠在一次提交里。
