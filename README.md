# XW

**XW —— 企业数字化平台与智能 Agent 应用实践**

> **说明**
>
> XW 源于企业数字化项目实践。  
> 因商业保密要求，部分业务模型、数据结构、算法规则及第三方集成细节已进行抽象或隐藏。  
> 本项目主要展示 **企业 AI 应用架构、Agent 设计及核心工程实践**。

XW 将 **业务中台、设备/运营能力与企业级智能 Agent** 统一在同一产品体系中：业务系统提供事实与执行，Agent 提供理解、编排与跨岗协作。

---

## 核心能力范围

- 企业业务中台架构
- AI 知识库 RAG
- LLM 应用接入
- Agent 能力模型设计
- Capability 工具体系
- 企业业务系统集成
- 权限与审计体系设计

---

## 为什么是 XW

| 挑战 | XW 的做法 |
|------|-------------|
| 进销存、客户、设备数据割裂 | 统一主数据与供应链 / 制造 / 销售履约闭环 |
| 业务数据难复用、运营难配置 | 模块化业务域 + 可发布的策略与配置快照 |
| 企业 AI 只会聊天、不能办事 | **组织协作型 Agent**：知识问答 + Capability 调用 + 跨岗流程与待办通知 |
| AI 直连库带来安全风险 | Agent 只经身份鉴权与能力白名单调用业务 API，全链路可审计 |

---

## 产品全景

```text
                    ┌─────────────────────────────────────┐
                    │        Console / 小程序 / 设备端       │
                    └─────────────────┬───────────────────┘
                                      │
          ┌───────────────────────────┼───────────────────────────┐
          ▼                           ▼                           ▼
   ┌──────────────┐          ┌──────────────┐          ┌──────────────────┐
   │  XW.Host   │          │ XW.Brain   │          │  XW.Agent      │
   │ 业务中台 API  │◄────────►│ 策略运行与     │          │ 企业智能 Agent   │
   │              │          │ 模型编排      │          │                  │
   └──────┬───────┘          └──────┬───────┘          └────────┬─────────┘
          │                         │                            │
          ▼                         ▼                            ▼
     平台业务库                  Brain 配置库              知识向量库 / 会话
          │
          └──────────► Integration（算法 / 报告统一形态）
```

| 产品面 | 说明 |
|--------|------|
| **业务中台** | 模块化单体：系统权限、主数据、产品、供应链、制造、销售、客户与策略、设备与诊测、门户等 |
| **策略运行与模型编排** | 独立 Host：策略快照消费、规则匹配与模型调用配置，与业务库边界清晰 |
| **算法集成** | 独立 Integration：可插拔厂商能力，统一结果形态回写业务侧 |
| **企业 Agent** | 对话入口、知识 RAG、业务 Capability、组织协作（流程 / 待办 / 通知）模型 |

---

## 业务中台架构

采用 **模块化单体（Modular Monolith）**：强一致业务本地事务；跨上下文仅依赖 `Application.Contracts`；AI 与算法能力外置为独立服务。

```text
Clients
  Console (Vue) ── /api/console
  小程序        ── /api/portal
  设备端        ── /api/device
        │
        ▼
   XW.Host
        │
   ┌────┴────┬─────────┬──────────┬─────────┬──────────┐
   System  MasterData Product  SupplyChain  MFG   Sales
   Customer  Devices   Portal   …（可扩展 Commerce / AfterSales）
        │
        ▼
   平台库 · 日志库
```

### 限界上下文（功能域）

| 模块 | 职责 |
|------|------|
| **System** | 员工、角色、菜单、RBAC |
| **MasterData** | 物料、仓库、客户主档与扩展、算法相关配置 |
| **Product** | 成品、BOM |
| **SupplyChain** | 采购、库存、SN、销售出库 |
| **Manufacturing** | 工单、领退料、产线单元 |
| **Sales** | 销售履约 |
| **Customer** | 策略配置、输出维度、权益包、LLM 目录与发布 |
| **Devices** | 设备档案、登录、诊测会话与结果 |
| **Portal** | C 端用户、激活与运营能力 |
| **Brain** | 策略运行时配置与解析（独立部署） |
| **Integration** | 外部算法路由与统一结果形态 |
| **Agent** | 企业对话、知识库、业务编排与协作 |

### 用户与 API 面

| 角色 | API | 鉴权 |
|------|-----|------|
| 平台员工 | `/api/console/*` | JWT |
| C 端用户 | `/api/portal/*` | 微信等 |
| 设备 | `/api/device/*` | Device Token |
| 企业 Agent | `/api/agent/*` | 调用方 Token / 会话上下文 |

---

## 调理推荐与诊测 AI

**策略配置** 与外部算法解耦：运营在 Console 配置推荐矩阵、权益与模型/提示词，发布为快照推送 Brain；运行时按客户 / Owner / 平台默认优先级解析。

**诊测与报告：** Devices 编排会话与算法快照 → Integration 按插件调用 → 统一结果回写业务侧；支持多模态扩展，新增能力以插件方式接入。

---

## Agent 定位

XW Agent **不是**传统聊天机器人，而是企业业务系统之上的 **智能交互与执行层**。

通过：

- **Knowledge RAG** — 企业知识查询与政策/FAQ 问答  
- **Capability** — 业务能力注册与安全调用  
- **Workflow** — 多步骤业务编排  
- **Task** — 跨岗位待办  
- **Notification** — 企微等触达与提醒  

实现企业知识查询、业务辅助及跨岗位协作。

**AI 负责理解与编排，业务系统负责事实与执行。**

---

## 企业智能 Agent

XW Agent 定位为 **企业内部操作系统式助手**：有权限边界、能问答、能调业务、支持跨人协作模型设计。

### 目标架构骨架

```text
                    用户
                     │
              Agent Gateway
                     │
              Conversation Agent
                     │
          ┌──────────┴──────────┐
          │                     │
      Knowledge             Flow Router
          │                     │
        RAG              Flow Runtime
                                │
                        Flow Instance
                                │
          ┌─────────────┬───────┴────────┐
          │             │                │
      Sales 域      Purchase 域      Service 域
   （Capability）  （Capability）   （Capability）
          │             │                │
         API           API              API

          Task Center · Notification · Event Bus · Resume Signal
          （组织协作能力模型 · 规划）
```

| 能力 | 说明 |
|------|------|
| **意图分流** | 咨询 / FAQ → 知识；办事与跨岗协作 → 流程；简单只读可直达对应业务 Capability |
| **知识问答（RAG）** | 向量检索 + 知识域隔离；面向政策、产品说明、故障 FAQ 等 |
| **业务 Capability** | 注册中心 + 动态检索；经 Integration 调业务 API，权限随执行人 |
| **结果裁定** | 成功 / 空结果 / 鉴权失败等结构化处理，保障回答与业务事实一致 |
| **组织协作模型** | 流程实例、跨人待办、通知与事件续跑（如缺货转采购、到货再继续） |

### 设计原则

1. **发起人愿望 ≠ 执行人权限** — 销售可发起「请采购补货」，由采购岗处理写操作。  
2. **LLM 是助手，流程与单据是主干** — 协作靠 Flow / Task / Signal 模型。  
3. **业务不直连库** — Agent 只经 Capability → 业务 API。  
4. **知识可演进为企业知识服务** — 检索与治理边界清晰，支撑多消费者与版本化运营。

---

## RAG Pipeline

```text
文档上传（md / txt）
  ↓
文本解析
  ↓
Domain 识别（domain）
  ↓
Chunk 切分
  ↓
Embedding（BGE-M3）
  ↓
pgvector 存储（vector 1024）
  ↓
TopK + Similarity 检索
  ↓
Context 构建
  ↓
LLM 回答
```

面向正式运营，知识体系进一步支持元数据约束检索、混合召回与精排、查询理解、效果评测，以及 **来源 → 权限 → 版本 → 生命周期 → 反馈** 的治理闭环，使政策更新时 Agent 依据当前有效版本作答。

---

## Agent 安全边界

Agent **不直接访问**业务数据库。

执行链路：

```text
User
  ↓
Identity
  ↓
Agent
  ↓
Capability
  ↓
Business API
  ↓
Audit
```

通过：

- 身份鉴权  
- 能力白名单  
- 业务接口二次鉴权  
- 审计  

保证执行安全。

---

## 技术栈

| 层级 | 选型 |
|------|------|
| 后端 | .NET / ASP.NET Core，模块化单体 + 独立 Host |
| 前端 | Vue 3 Console；小程序 / 设备端按业务接入 |
| 数据 | MySQL（业务与日志）；PostgreSQL + pgvector（Agent 知识） |
| AI 基础 | Embedding、RAG Pipeline、OpenAI Compatible API |
| 集成 | 消息 / HTTP 网关、可插拔集成、OpenAI 兼容 LLM / Embedding |
| 可观测 | OpenTelemetry |
| 本地编排 | .NET Aspire AppHost（可选） |

---

## 仓库结构（概览）

```text
XW/
├── src/
│   ├── Modules/          # 业务限界上下文
│   ├── Services/         # Host、Brain、Integration、Agent 等可运行服务
│   ├── Aspire/           # 本地应用编排
│   └── Infrastructures/  # 基础设施与共享内核
├── client/               # Console 等前端
└── README.md             # 本说明
```

---

## 设计哲学

- **边界清晰** — 业务库与 Brain 库、Agent 与业务 API 各守其职。  
- **可演进** — 模块化单体支撑交付；Agent / 知识 / 算法可独立扩展为平台能力。  
- **对企业真实** — 权限、待办、通知、版本化知识优先于「更会聊天」。  
- **AI 编排、系统执行** — 理解与编排在 Agent，事实与写操作在业务系统。  
- **业务优先** — Agent 不替代业务系统，而是在业务系统之上提供智能入口。

---

**XW** —— 企业数字化平台与智能 Agent 应用实践。
