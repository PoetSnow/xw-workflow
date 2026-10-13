# 审批时间线与提交即解析 — 方案

对照：流程设计器（画布）只负责配置；本方案负责「单据提交后怎么看、怎么批、待办何时进箱」。  
运行时仍只有 `ApproveInstance` 一个聚合根。业务模块只依赖 `XW.Approve.Application.Contracts`。

---

## 1. 目标

提交审批后，单据页立刻能看到这一单会走的完整环节和每一环的人，从上到下排列，不画连线。

当前登录人若是当前环审批人，在自己那一行通过 / 驳回；发起人可撤销。

路径和人在提交时一次算完、落库。待办工作记录只在单据真正转到那一环时出现在「我的待办」。

---

## 2. 两套界面，不要混

| | 流程设计器 | 单据时间线 |
| --- | --- | --- |
| 谁用 | 管理员 | 业务办理人 |
| 画什么 | 可配置图：节点、连线、条件、找人规则 | 这一单实际路径：一环接一环 |
| 连线 | 要，用来配 XOR 分支 | 不要 |
| 人 | 规则（角色 / 部门内角色 / 指定用户） | 已经解析出的具体人 |
| 操作 | 保存 / 发布 | 通过 / 驳回 / 撤销 |

设计器继续用现有 `/approve/processes`。时间线是新组件，读新接口。

---

## 3. 口径

1. 提交时用表单快照沿 XOR 走出唯一路径。金额不到阈值就不含总经理，不是把没走的分支也画出来。
2. 路径上每一环立刻写成 `appr_instance_node`，审批人按当时角色和部门解析，写成 `appr_task`。某一环一个人都没有，提交失败，单据不出去。
3. **已解析 ≠ 已有待办。** 后面几环的人提交时就能在时间线上看到，但不进待办箱，也不能批。
4. 上一环通过后，不再重新找人，只激活下一环已存的人：任务变为 `Pending`，进入「我的待办」。
5. 角色后来加人、减人，不影响已经发出的这一单。
6. 按钮不按角色猜。后端标明 `canApprove` / `canCancel`，组件才渲染。
7. 会签、或签叠在同一环。每人一条任务；或签一人通过，其余 `Skipped`。

---

## 4. 提交与流转

### 4.1 提交 `ApproveInstance.Start`

1. 按绑定取已发布流程版本。
2. 用快照（含 `DeptId`）从开始节点走出唯一路径，直到结束节点。超过最大跳数失败。
3. 开始节点：状态 `Approved`，写 `Submit` 历史。
4. 每一个审批节点：落节点 + 落全部解析出的人。
5. 第一环审批：节点 `Running`，任务 `Pending`，`CurrentNodeId` 指向它。这些人进待办箱。
6. 后续审批：节点 `Pending`，任务 `Scheduled`。时间线可见，待办箱不可见。
7. 结束节点：节点 `Pending`，不派人。
8. 任一审批节点解析结果为空 → 抛领域异常，整单不落库。

### 4.2 通过后转到下一环

当前环判定通过后：

1. 本环未处理的 `Pending` 标 `Skipped`。
2. 按已落库的节点顺序取下一环，不再跑找人规则。
3. 下一环是结束：节点 `Approved`，实例 `Approved`，通知业务模块。
4. 下一环是审批：节点 `Running`，该环 `Scheduled` 改为 `Pending`，`CreateTime` / 节点 `StartTime` 记到达时间。这些人此时才出现在「我的待办」。
5. `CurrentNodeId` 指向新环。

### 4.3 驳回 / 撤销

- 驳回：当前任务 `Rejected`，本环其余 `Pending` 取消，未到达节点和 `Scheduled` 任务一并 `Cancelled`，实例 `Rejected`。
- 撤销：仅发起人。所有 `Pending` / `Scheduled` 取消，进行中和未到达节点 `Cancelled`，实例 `Cancelled`。

两种情况都不会在事后给未到达的人冒出待办。

---

## 5. 状态

节点（已有，不新增）：

| 状态 | 含义 |
| --- | --- |
| `Pending` | 路径上已生成，单据还没到 |
| `Running` | 当前环 |
| `Approved` / `Rejected` / `Cancelled` | 结束态 |

任务：新增 `Scheduled`。

| 状态 | 含义 | 进待办箱 | 时间线按钮 |
| --- | --- | --- | --- |
| `Scheduled` | 人已定，环节未到 | 否 | 否 |
| `Pending` | 环节已到，待处理 | 是 | 仅当前用户自己那条 |
| `Approved` / `Rejected` | 已处理 | 否 | 否 |
| `Skipped` | 或签已被别人过掉 | 否 | 否 |
| `Cancelled` | 驳回或撤销 | 否 | 否 |

待办查询继续：`AssigneeUserId = 当前用户` 且 `Status = Pending` 且实例 `Running`。不用改筛选字段。

库表不用加列。`appr_task.status`、`appr_instance_node.status` 已是字符串。

---

## 6. API

写接口保持按任务，不按节点。改提交行为，并加一条给组件用的时间线。

### 6.1 已有，行为有变化

| 接口 | 变化 |
| --- | --- |
| `POST /approve/instances` | 入参不变。成功后库里已是完整路径和人，第一环已有 `Pending` 待办。 |
| `POST /approve/tasks/{taskId}/approve` | 通过后激活下一环已存任务，不再解析。 |
| `POST /approve/tasks/{taskId}/reject` | 取消未到达的节点和 `Scheduled`。 |
| `POST /approve/instances/{id}/cancel` | 同上。 |
| `GET /approve/tasks/mine` | 仍只返回 `Pending`。 |

`GET /approve/instances/{id}` 保留，给后台排查。组件不读它。

### 6.2 新增时间线

```http
GET /approve/timeline?businessType=Purchase&businessId={单据主键}
```

单据页通常没有实例 Id，用业务类型 + 单据主键。没有记录返回 404，组件显示空，不报红。

可选：`GET /approve/instances/{id}/timeline`，待办页已有实例 Id 时用。

```text
ApproveTimelineDto
  instanceId
  title
  status                 Running / Approved / Rejected / Cancelled
  canCancel              当前用户是发起人且进行中
  steps[]
    nodeId
    nodeName
    nodeType             Start / Approve / End
    approveMode          Single / OrSign / AndSign / Vote
    status               Pending / Running / Approved / Rejected / Cancelled
    isCurrent
    startTime / endTime
    assignees[]
      taskId
      userId
      userName
      roleName
      status             Scheduled / Pending / Approved / Rejected / Skipped / Cancelled
      comment
      completeTime
      canApprove         当前用户 + 当前环 + 任务 Pending
```

姓名、角色名、`canApprove`、`canCancel` 都在服务端填。组件不拼节点和历史，不算分支。

开始环：`assignees` 放发起人，意见可用提交标题或空，时间用 `StartTime`。结束环：无人，只作终点标记，页面可收成「结束」。

---

## 7. 组件

路径：`NX-AIHealth-Platform/src/components/ApproveTimeline/index.vue`

```vue
<ApproveTimeline
  business-type="Purchase"
  :business-id="order.id"
  @changed="reloadOrder"
/>
```

待办打开时也可传 `instance-id`。

竖排时间线，一环一个环节：

1. 发起人、提交时间；`canCancel` 为真时这里放「撤销」。
2. 已完成：人名、通过或驳回、意见、处理时间。
3. 当前环：待处理的人；`canApprove` 为真的那一行：意见框 + 通过 + 驳回。
4. 未到达：已解析的人，只显示名字。

组件自己拉时间线，自己调通过 / 驳回 / 撤销，成功后重拉并 `emit('changed')`。页面只刷新单据状态。不依赖设计器，不画线。

没有审批记录：不渲染或一行「尚未提交审批」。

---

## 8. 接到单据上

第一单仍是采购。采购单详情（或抽屉）提交成功后嵌 `ApproveTimeline`。页面继续负责组快照、调 `SubmitAsync`。组件负责展示和审批操作。

待办箱点开一条，同样嵌这个组件，用 `instance-id` 或 `businessType + businessId`。

采购提交审批、结束回调（通过 → 确认，驳回 / 撤销 → 回草稿）仍按原任务计划，本方案不改业务状态机，只保证提交当时路径和人已经在库里。

---

## 9. 实现顺序

1. Domain：`ApproveTaskStatus.Scheduled`；`Start` 走出完整路径并落节点和人；第一环 `Pending`，其后 `Scheduled`。
2. Domain：通过后只激活下一环已存任务；驳回 / 撤销清掉未到达记录。
3. 待办查询保持只认 `Pending`。补单测：提交后后面的人有记录但不在待办箱；通过后下一环进入待办箱。
4. Application + API：时间线 DTO 与 `GET /approve/timeline`，补姓名。
5. 前端 `ApproveTimeline`，接到采购单详情。
6. 浏览器走通：设计器发布采购流程 → 提交一单 → 单据页看到全路径 → 第一环待办可批 → 通过后下一环出现待办。

---

## 10. 本期不做

- 时间线画设计器那种连线和条件分支。
- 转交 / 加签 / 委托（表字段已预留）。
- 未到达环节改人。
- 把 `ProcessDef` 升级成聚合根。
- 一人多部门。

设计器继续只改草稿、发布出新版本；在途实例仍绑定提交时的 `ProcessDefId`。
